#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Executor;

namespace GamePlatform.Storage.Sqlite.Outbox
{
    internal enum AdmissionCheckpoint { ProjectionApplied, SequenceAllocated, OutboxInserted }

    public enum StreamRecoveryState { Current, LocalGap, ServerAheadOfLocalState }

    public readonly struct StreamRecoveryAssessment
    {
        public StreamRecoveryAssessment(StreamRecoveryState state, long localNextSequence, long serverFinalizedThrough)
        {
            State = state;
            LocalNextSequence = localNextSequence;
            ServerFinalizedThrough = serverFinalizedThrough;
        }
        public StreamRecoveryState State { get; }
        public long LocalNextSequence { get; }
        public long ServerFinalizedThrough { get; }
    }

    public sealed class SqliteAtomicCommandStore : IAtomicCommandStore
    {
        private readonly SqliteDatabase database;
        private readonly StorageScope scope;
        private readonly OwnerScope owner;
        private readonly ICommandFingerprint fingerprint;
        private readonly IUnixMillisecondClock clock;
        private readonly Action<AdmissionCheckpoint>? checkpoint;

        public SqliteAtomicCommandStore(SqliteDatabase database, StorageScope scope, OwnerScope owner, ICommandFingerprint fingerprint, IUnixMillisecondClock clock)
            : this(database, scope, owner, fingerprint, clock, null) { }

        internal SqliteAtomicCommandStore(SqliteDatabase database, StorageScope scope, OwnerScope owner, ICommandFingerprint fingerprint, IUnixMillisecondClock clock, Action<AdmissionCheckpoint>? checkpoint)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (!owner.IsValid) throw new ArgumentException("A valid owner is required.", nameof(owner));
            if (!Matches(scope, owner)) throw new StorageException(StorageFailure.InvalidOwner, "The command owner does not match the database scope.");
            this.scope = scope;
            this.owner = owner;
            this.checkpoint = checkpoint;
        }

        public Task<CommandAdmission> CommitAsync(
            string businessRunId,
            CommandDraft command,
            Action<ILocalStorageTransaction, long> applyProjection,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(businessRunId) || businessRunId.Length > 256)
                throw new ArgumentOutOfRangeException(nameof(businessRunId));
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (applyProjection == null) throw new ArgumentNullException(nameof(applyProjection));
            if (command.Owner != owner) throw new StorageException(StorageFailure.InvalidOwner, "The command owner does not match the store owner.");
            return database.ExecuteAsync(scope, transaction => CommitCore(
                (SqliteTransactionSession)transaction, businessRunId, command, applyProjection, cancellationToken), cancellationToken);
        }

        public Task<StreamRecoveryAssessment> AssessRecoveryAsync(long serverFinalizedThrough, CancellationToken cancellationToken)
        {
            if (serverFinalizedThrough < 0) throw new ArgumentOutOfRangeException(nameof(serverFinalizedThrough));
            return database.ExecuteAsync(scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                EnsureReady(session, default);
                var next = session.ExecuteScalar<long>("SELECT next_sequence FROM gp_stream_state WHERE singleton = 1");
                if (serverFinalizedThrough >= next)
                    return new StreamRecoveryAssessment(StreamRecoveryState.ServerAheadOfLocalState, next, serverFinalizedThrough);
                if (next == serverFinalizedThrough + 1)
                    return new StreamRecoveryAssessment(StreamRecoveryState.Current, next, serverFinalizedThrough);
                var missing = session.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM (WITH RECURSIVE expected(value) AS (SELECT ? + 1 UNION ALL SELECT value + 1 FROM expected WHERE value + 1 < ?) SELECT value FROM expected EXCEPT SELECT sequence FROM gp_outbox WHERE sequence > ? AND sequence < ?)",
                    serverFinalizedThrough, next, serverFinalizedThrough, next);
                return new StreamRecoveryAssessment(missing == 0 ? StreamRecoveryState.Current : StreamRecoveryState.LocalGap, next, serverFinalizedThrough);
            }, cancellationToken);
        }

        public Task<int> RecoverExpiredLeasesAsync(long nowMilliseconds, CancellationToken cancellationToken)
        {
            if (nowMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));
            return database.ExecuteAsync(scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                EnsureReady(session, default);
                return session.Execute("UPDATE gp_outbox SET delivery_state = 'pending', leased_until = NULL WHERE delivery_state = 'in_flight' AND leased_until <= ?", nowMilliseconds);
            }, cancellationToken);
        }

        private CommandAdmission CommitCore(
            SqliteTransactionSession session,
            string businessRunId,
            CommandDraft command,
            Action<ILocalStorageTransaction, long> applyProjection,
            CancellationToken cancellationToken)
        {
            EnsureReady(session, command.StreamId);
            var existingByOperation = session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox WHERE operation_id = ?", command.OperationId.ToString());
            var existingByRun = session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox WHERE business_run_id = ?", businessRunId);
            if (existingByOperation != 0 || existingByRun != 0)
                return ReadIdempotentAdmission(session, businessRunId, command);

            var next = session.ExecuteScalar<long>("SELECT next_sequence FROM gp_stream_state WHERE singleton = 1");
            var revision = session.ExecuteScalar<long>("SELECT local_revision FROM gp_stream_state WHERE singleton = 1");
            var installation = new Guid(session.ExecuteScalar<string>("SELECT installation_id FROM gp_stream_state WHERE singleton = 1"));
            if (!UuidIdentity.IsValid(installation))
                throw new StorageException(StorageFailure.Constraint, "The durable stream installation identity is invalid.");
            var clientCreatedAt = PlatformNumbers.UnixMilliseconds(clock.GetUnixMilliseconds());
            if (next <= 0 || next == long.MaxValue || revision == long.MaxValue)
                throw new StorageException(StorageFailure.SequenceExhausted, "The durable command sequence or local revision is exhausted.");
            var localRevision = checked(revision + 1);
            applyProjection(session, localRevision);
            checkpoint?.Invoke(AdmissionCheckpoint.ProjectionApplied);
            cancellationToken.ThrowIfCancellationRequested();

            session.Execute("UPDATE gp_stream_state SET next_sequence = ?, local_revision = ? WHERE singleton = 1", checked(next + 1), localRevision);
            checkpoint?.Invoke(AdmissionCheckpoint.SequenceAllocated);

            var body = CopyBody(command);
            var fingerprintLength = fingerprint.GetFingerprintLength(command.FingerprintVersion);
            if (fingerprintLength <= 0 || fingerprintLength > 512)
                throw new StorageException(StorageFailure.Constraint, "The fingerprint length is invalid.");
            var finalFingerprint = new byte[fingerprintLength];
            fingerprint.Compute(owner, command.OperationId, command.StreamId, installation, next, command.OperationKind,
                command.SchemaVersion, command.FingerprintVersion, clientCreatedAt, body, finalFingerprint);
            session.Execute(
                "INSERT INTO gp_outbox(operation_id, backend_namespace, app_id, account_id, client_stream_id, installation_id, sequence, business_run_id, operation_kind, schema_version, fingerprint_version, client_created_at, semantic_body, fingerprint, local_revision) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                command.OperationId.ToString(), owner.Backend.Value, owner.AppId.ToString(), owner.UserId.ToString(),
                command.StreamId.ToString(), installation.ToString("D"), next, businessRunId, command.OperationKind, command.SchemaVersion,
                command.FingerprintVersion, clientCreatedAt, body, finalFingerprint, localRevision);
            checkpoint?.Invoke(AdmissionCheckpoint.OutboxInserted);
            cancellationToken.ThrowIfCancellationRequested();
            return new CommandAdmission(command.OperationId, command.StreamId, installation, clientCreatedAt, next, localRevision);
        }

        private CommandAdmission ReadIdempotentAdmission(SqliteTransactionSession session, string businessRunId, CommandDraft command)
        {
            var operationId = session.ExecuteScalar<string>("SELECT operation_id FROM gp_outbox WHERE operation_id = ? OR business_run_id = ? LIMIT 1", command.OperationId.ToString(), businessRunId);
            var run = session.ExecuteScalar<string>("SELECT business_run_id FROM gp_outbox WHERE operation_id = ? OR business_run_id = ? LIMIT 1", command.OperationId.ToString(), businessRunId);
            var stream = session.ExecuteScalar<string>("SELECT client_stream_id FROM gp_outbox WHERE operation_id = ? OR business_run_id = ? LIMIT 1", command.OperationId.ToString(), businessRunId);
            var kind = session.ExecuteScalar<string>("SELECT operation_kind FROM gp_outbox WHERE operation_id = ? OR business_run_id = ? LIMIT 1", command.OperationId.ToString(), businessRunId);
            var schema = session.ExecuteScalar<int>("SELECT schema_version FROM gp_outbox WHERE operation_id = ? OR business_run_id = ? LIMIT 1", command.OperationId.ToString(), businessRunId);
            var fingerprintVersion = session.ExecuteScalar<int>("SELECT fingerprint_version FROM gp_outbox WHERE operation_id = ? OR business_run_id = ? LIMIT 1", command.OperationId.ToString(), businessRunId);
            var body = session.ExecuteScalar<byte[]>("SELECT semantic_body FROM gp_outbox WHERE operation_id = ? OR business_run_id = ? LIMIT 1", command.OperationId.ToString(), businessRunId);
            if (!string.Equals(operationId, command.OperationId.ToString(), StringComparison.Ordinal) ||
                !string.Equals(run, businessRunId, StringComparison.Ordinal) ||
                !string.Equals(stream, command.StreamId.ToString(), StringComparison.Ordinal) ||
                !string.Equals(kind, command.OperationKind, StringComparison.Ordinal) || schema != command.SchemaVersion ||
                fingerprintVersion != command.FingerprintVersion || !Equal(body, CopyBody(command)))
                throw new StorageException(StorageFailure.IdentityConflict, "The operation or business run was already admitted with different immutable semantics.");
            var sequence = session.ExecuteScalar<long>("SELECT sequence FROM gp_outbox WHERE operation_id = ?", operationId);
            var revision = session.ExecuteScalar<long>("SELECT local_revision FROM gp_outbox WHERE operation_id = ?", operationId);
            var installation = new Guid(session.ExecuteScalar<string>("SELECT installation_id FROM gp_outbox WHERE operation_id = ?", operationId));
            var clientCreatedAt = session.ExecuteScalar<long>("SELECT client_created_at FROM gp_outbox WHERE operation_id = ?", operationId);
            return new CommandAdmission(command.OperationId, command.StreamId, installation, clientCreatedAt, sequence, revision);
        }

        private void EnsureReady(SqliteTransactionSession session, ClientStreamId stream)
        {
            var count = session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_stream_state WHERE singleton = 1 AND backend_namespace = ? AND app_id = ? AND account_id = ? AND ready = 1", owner.Backend.Value, owner.AppId.ToString(), owner.UserId.ToString());
            if (count != 1) throw new StorageException(StorageFailure.NotReady, "Owned mutation requires a Ready stream.");
            if (stream.IsValid)
            {
                var streamMatches = session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_stream_state WHERE singleton = 1 AND client_stream_id = ?", stream.ToString());
                if (streamMatches != 1) throw new StorageException(StorageFailure.StreamRecoveryRequired, "The command stream does not match durable state.");
            }
        }

        private static byte[] CopyBody(CommandDraft command)
        {
            var body = new byte[command.SemanticBodyLength];
            command.CopySemanticBodyTo(body);
            return body;
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left == null || left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }

        private static bool Matches(StorageScope scope, OwnerScope owner) =>
            string.Equals(scope.BackendNamespace, owner.Backend.Value, StringComparison.Ordinal) &&
            string.Equals(scope.AppId.Value, owner.AppId.ToString(), StringComparison.Ordinal) &&
            string.Equals(scope.AccountId.Value, owner.UserId.ToString(), StringComparison.Ordinal);
    }
}
