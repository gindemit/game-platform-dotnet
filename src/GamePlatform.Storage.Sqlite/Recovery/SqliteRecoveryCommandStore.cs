#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Abstractions.Recovery;
using GamePlatform.Storage.Sqlite.Executor;

namespace GamePlatform.Storage.Sqlite.Recovery
{
    /// <summary>Quarantines the retained database, without changing sequences or confirmed projections.</summary>
    public sealed class SqliteRecoveryCommandStore : IRecoveryCommandStore
    {
        private readonly SqliteDatabase database;
        private readonly StorageScope scope;
        private readonly Guid installationId;
        private readonly bool recreatedAndEmpty;

        public SqliteRecoveryCommandStore(SqliteDatabase database, StorageScope scope, Guid installationId, bool verifiedRecreatedAndEmpty = false)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            if (!database.Scope.Equals(scope)) throw new StorageException(StorageFailure.InvalidOwner, "Recovery storage belongs to another owner.");
            if (!UuidIdentity.IsValid(installationId)) throw new ArgumentException("A valid installation is required.", nameof(installationId));
            this.scope = scope;
            this.installationId = installationId;
            recreatedAndEmpty = verifiedRecreatedAndEmpty;
        }

        public async Task<IReadOnlyList<LeasedCommand>> SuspendAndReadAsync(ClientStreamId priorStreamId, CancellationToken cancellationToken)
        {
            if (!priorStreamId.IsValid) throw new ArgumentException("A prior stream is required.", nameof(priorStreamId));
            await database.ExecuteAsync(scope, transaction =>
            {
                var s = (SqliteTransactionSession)transaction;
                RequireOwner(s, priorStreamId);
                s.Execute("UPDATE gp_stream_state SET ready=0 WHERE singleton=1");
                s.Execute("UPDATE gp_sync_state SET ready=0 WHERE singleton=1");
                return true;
            }, cancellationToken).ConfigureAwait(false);
            return await database.ExecuteAsync<IReadOnlyList<LeasedCommand>>(scope, transaction =>
            {
                var s = (SqliteTransactionSession)transaction;
                RequireOwner(s, priorStreamId);
                RequireSuspended(s);
                // Bound before materializing payloads; capacity failures leave the earlier suspension committed.
                if (s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox WHERE delivery_state IN ('pending','in_flight')") > 256 ||
                    s.ExecuteScalar<long>("SELECT COALESCE(SUM(length(semantic_body)+length(fingerprint)),0) FROM gp_outbox WHERE delivery_state IN ('pending','in_flight')") > 8_388_608)
                    throw new StorageException(StorageFailure.StreamRecoveryRequired, "The retained command backlog requires a bounded recovery handoff.");
                var rows = s.Query<CommandRow>("SELECT operation_id AS OperationId,client_stream_id AS StreamId,installation_id AS InstallationId,sequence AS Sequence,operation_kind AS OperationKind,schema_version AS SchemaVersion,fingerprint_version AS FingerprintVersion,client_created_at AS CreatedAt,semantic_body AS Body,fingerprint AS Fingerprint FROM gp_outbox WHERE delivery_state IN ('pending','in_flight') ORDER BY sequence");
                var commands = new LeasedCommand[rows.Count];
                for (var i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    if (r.StreamId != priorStreamId.ToString() || r.InstallationId != installationId.ToString("D"))
                        throw new StorageException(StorageFailure.InvalidOwner, "A retained command belongs to another stream or installation.");
                    commands[i] = new LeasedCommand(new OperationId(Guid.Parse(r.OperationId)), priorStreamId, installationId,
                        r.Sequence, r.OperationKind, r.SchemaVersion, r.FingerprintVersion, r.CreatedAt, r.Body, r.Fingerprint);
                }
                return Array.AsReadOnly(commands);
            }, cancellationToken).ConfigureAwait(false);
        }

        public Task RecordReceiptAsync(LeasedCommand command, CommandTerminalOutcome outcome, byte[]? result, CancellationToken cancellationToken)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (command.InstallationId != installationId || (result?.Length ?? 0) > 65_536 ||
                (outcome != CommandTerminalOutcome.Accepted && outcome != CommandTerminalOutcome.Rejected))
                throw new ArgumentException("A valid retained receipt is required.");
            var copy = result == null ? null : (byte[])result.Clone();
            return database.ExecuteAsync(scope, transaction =>
            {
                var s = (SqliteTransactionSession)transaction;
                RequireOwner(s, command.StreamId);
                RequireSuspended(s);
                if (s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox WHERE operation_id=? AND client_stream_id=? AND installation_id=? AND sequence=? AND fingerprint=? AND semantic_body=? AND operation_kind=? AND schema_version=? AND fingerprint_version=? AND client_created_at=?", command.OperationId.ToString(), command.StreamId.ToString(), installationId.ToString("D"), command.Sequence, command.CopyFingerprint(), command.CopyBody(), command.OperationKind, command.SchemaVersion, command.FingerprintVersion, command.ClientCreatedAt) != 1)
                    throw new StorageException(StorageFailure.StreamRecoveryRequired, "Receipt identity does not match retained evidence.");
                var state = outcome == CommandTerminalOutcome.Accepted ? "accepted" : "terminal_rejected";
                var current = s.ExecuteScalar<string>("SELECT delivery_state FROM gp_outbox WHERE operation_id=?", command.OperationId.ToString());
                if (current == "accepted" || current == "terminal_rejected")
                {
                    if (current != state || s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox WHERE operation_id=? AND terminal_result IS ?", command.OperationId.ToString(), copy!) != 1)
                        throw new StorageException(StorageFailure.StreamRecoveryRequired, "A retained terminal receipt changed.");
                    return true;
                }
                s.Execute("UPDATE gp_outbox SET delivery_state=?,leased_until=NULL,terminal_result=? WHERE operation_id=?", state, copy!, command.OperationId.ToString());
                return true;
            }, cancellationToken);
        }

        public Task<bool> IsSafeToRetireAsync(ClientStreamId priorStreamId, CancellationToken cancellationToken) =>
            database.ExecuteAsync(scope, transaction =>
            {
                var s = (SqliteTransactionSession)transaction;
                RequireOwner(s, priorStreamId);
                RequireSuspended(s);
                return s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox WHERE delivery_state IN ('pending','in_flight')") == 0;
            }, cancellationToken);

        private void RequireOwner(SqliteTransactionSession s, ClientStreamId priorStreamId)
        {
            if (!priorStreamId.IsValid) throw new ArgumentException("A retained prior identity is required.", nameof(priorStreamId));
            if (recreatedAndEmpty && s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_stream_state") == 0 &&
                s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox") == 0 &&
                s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_sync_state") == 0 &&
                s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_bootstrap_state") == 0 &&
                s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_bootstrap_entities") == 0 &&
                s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_bootstrap_pages") == 0 &&
                s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state") == 0 &&
                s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_accounts_directory") == 0 &&
                s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_extension_migrations") == 0 &&
                s.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT IN ('gp_database_scope','gp_migrations','gp_stream_state','gp_outbox','gp_sync_state','gp_confirmed_projection','gp_bootstrap_state','gp_bootstrap_entities','gp_bootstrap_pages','gp_feature_state','gp_accounts_directory','gp_extension_migrations')") == 0 &&
                s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_confirmed_projection") == 0)
                return; // Explicit host damage evidence plus retained directory identity; never manufacture a prior stream row.
            if (s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_stream_state WHERE singleton=1 AND client_stream_id=? AND installation_id=?", priorStreamId.ToString(), installationId.ToString("D")) != 1)
                throw new StorageException(StorageFailure.InvalidOwner, "The prior stream or installation is unavailable.");
            if (s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_outbox WHERE backend_namespace<>? OR app_id<>? OR account_id<>? OR client_stream_id<>? OR installation_id<>?", scope.BackendNamespace, scope.AppId.ToString(), scope.AccountId.ToString(), priorStreamId.ToString(), installationId.ToString("D")) != 0)
                throw new StorageException(StorageFailure.InvalidOwner, "Retained commands belong to another owner.");
        }

        private static void RequireSuspended(SqliteTransactionSession s)
        {
            if (s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_stream_state WHERE ready<>0") != 0 ||
                s.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_sync_state WHERE ready<>0") != 0)
                throw new StorageException(StorageFailure.StreamRecoveryRequired, "Prior writers must remain suspended.");
        }

        private sealed class CommandRow
        {
            public string OperationId { get; set; } = "";
            public string StreamId { get; set; } = "";
            public string InstallationId { get; set; } = "";
            public long Sequence { get; set; }
            public string OperationKind { get; set; } = "";
            public int SchemaVersion { get; set; }
            public int FingerprintVersion { get; set; }
            public long CreatedAt { get; set; }
            public byte[] Body { get; set; } = Array.Empty<byte>();
            public byte[] Fingerprint { get; set; } = Array.Empty<byte>();
        }
    }
}
