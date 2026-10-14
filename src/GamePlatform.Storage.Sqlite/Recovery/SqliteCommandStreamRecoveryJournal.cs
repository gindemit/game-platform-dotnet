#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Abstractions.Recovery;
using GamePlatform.Storage.Sqlite.Features.Accounts;
using SQLite;

namespace GamePlatform.Storage.Sqlite.Recovery
{
    /// <summary>
    /// Persists command stream recovery next to the principal directory, outside the recreatable gameplay database.
    /// </summary>
    public sealed class SqliteCommandStreamRecoveryJournal : ICommandStreamRecoveryJournal
    {
        private readonly SqliteAccountsDirectoryDatabase database;
        private readonly AccountDirectoryEntry owner;
        private readonly AccountDirectoryScope scope;

        public SqliteCommandStreamRecoveryJournal(SqliteAccountsDirectoryDatabase database, AccountDirectoryEntry issuedEntry)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            owner = issuedEntry ?? throw new ArgumentNullException(nameof(issuedEntry));
            if (!owner.HasIssuedAccount || !owner.Principal.BackendNamespace.IsValid || !owner.AppId.IsValid ||
                !UuidIdentity.IsValid(owner.InstallationId) || !owner.StreamId.IsValid)
                throw new ArgumentException("An issued account directory entry is required.", nameof(issuedEntry));
            scope = new AccountDirectoryScope(owner.Principal.BackendNamespace, owner.AppId);
            if (!database.Scope.Equals(scope))
                throw new StorageException(StorageFailure.InvalidOwner, "The recovery journal owner does not match its directory database.");
        }

        public Task<CommandStreamRecoveryRecord?> LoadAsync(CancellationToken cancellationToken) =>
            database.ExecuteAsync(scope, connection => Load(connection, true), cancellationToken);

        public Task<CommandStreamRecoveryRecord> PrepareAsync(CommandStreamRecoveryRequest request, CancellationToken cancellationToken)
        {
            ValidateRequestOwner(request);
            return database.ExecuteAsync(scope, connection => InCallerTransaction(connection, cancellationToken, () =>
            {
                var existing = ReadRecord(connection, request.RecoveryId, false);
                if (existing != null)
                {
                    if (SameRequest(existing.Request, request))
                    {
                        EnsureDirectory(connection, existing.Phase >= CommandStreamRecoveryPhase.Adopted ? existing.Request.SuccessorStreamId : existing.Request.PriorStreamId, true);
                        return existing;
                    }
                    throw Conflict("The recovery ID is already bound to another immutable request.");
                }
                EnsureDirectory(connection, request.PriorStreamId, requireOwnerStream: true);
                var pending = ReadLatest(connection);
                if (pending != null && pending.Phase != CommandStreamRecoveryPhase.BootstrapComplete && pending.Phase != CommandStreamRecoveryPhase.Expired)
                    throw Conflict("A different stream recovery is already pending.");
                connection.Execute("INSERT INTO gp_command_stream_recovery(backend_namespace,app_id,issuer,subject,platform_user_id,installation_id,prior_stream_id,successor_stream_id,recovery_id,reason,inspection_token,expected_finalized_through,phase,remote_next_sequence,remote_prior_finalized_through) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,1,NULL,NULL)",
                    scope.BackendNamespace.Value, owner.AppId.ToString(), owner.Principal.Issuer, owner.Principal.Subject, owner.AccountId!.Value.ToString(),
                    owner.InstallationId.ToString("D"), request.PriorStreamId.ToString(), request.SuccessorStreamId.ToString(), request.RecoveryId.ToString("D"),
                    request.Reason, request.InspectionToken, request.ExpectedFinalizedThrough);
                return ReadRecord(connection, request.RecoveryId, true)!;
            }), cancellationToken);
        }

        public Task<CommandStreamRecoveryRecord> RecordRemoteCommitAsync(Guid recoveryId, Guid returnedRecoveryId, ClientStreamId priorStreamId,
            ClientStreamId successorStreamId, long priorFinalizedThrough, long nextSequence, CancellationToken cancellationToken) =>
            database.ExecuteAsync(scope, connection => InCallerTransaction(connection, cancellationToken, () =>
            {
                var record = RequireRecord(connection, recoveryId);
                if (returnedRecoveryId != recoveryId)
                    throw Conflict("The server recovery result ID does not match the durable request.");
                ValidateRemoteResult(record.Request, priorStreamId, successorStreamId, priorFinalizedThrough, nextSequence);
                if (record.Phase >= CommandStreamRecoveryPhase.RemoteCommitted)
                    return record;
                if (record.Phase != CommandStreamRecoveryPhase.Prepared)
                    throw Conflict("The recovery journal is in an invalid phase.");
                EnsureDirectory(connection, record.Request.PriorStreamId, requireOwnerStream: true);
                var updated = connection.Execute("UPDATE gp_command_stream_recovery SET phase=2,remote_next_sequence=1,remote_prior_finalized_through=? WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND recovery_id=? AND phase=1",
                    priorFinalizedThrough, scope.BackendNamespace.Value, owner.AppId.ToString(), owner.Principal.Issuer, owner.Principal.Subject, recoveryId.ToString("D"));
                if (updated != 1)
                    throw Conflict("The recovery journal changed while recording the remote result.");
                return ReadRecord(connection, recoveryId, true)!;
            }), cancellationToken);

        public Task<CommandStreamRecoveryRecord> MarkExpiredAsync(Guid recoveryId, CancellationToken cancellationToken) =>
            database.ExecuteAsync(scope, connection => InCallerTransaction(connection, cancellationToken, () =>
            {
                var record = RequireRecord(connection, recoveryId);
                EnsureDirectory(connection, record.Request.PriorStreamId, requireOwnerStream: true);
                if (record.Phase == CommandStreamRecoveryPhase.Expired)
                    return record;
                if (record.Phase != CommandStreamRecoveryPhase.Prepared)
                    throw Conflict("Only an uncommitted prepared recovery may expire.");
                var updated = connection.Execute("UPDATE gp_command_stream_recovery SET phase=0 WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND recovery_id=? AND phase=1",
                    scope.BackendNamespace.Value, owner.AppId.ToString(), owner.Principal.Issuer, owner.Principal.Subject, recoveryId.ToString("D"));
                if (updated != 1)
                    throw Conflict("The recovery journal changed while recording expiry.");
                return ReadRecord(connection, recoveryId, true)!;
            }), cancellationToken);

        public Task<CommandStreamRecoveryRecord> AdoptAsync(Guid recoveryId, CancellationToken cancellationToken) =>
            database.ExecuteAsync(scope, connection => InCallerTransaction(connection, cancellationToken, () =>
            {
                var record = RequireRecord(connection, recoveryId);
                if (record.Phase >= CommandStreamRecoveryPhase.Adopted)
                {
                    EnsureDirectory(connection, record.Request.SuccessorStreamId, requireOwnerStream: true);
                    return record;
                }
                if (record.Phase != CommandStreamRecoveryPhase.RemoteCommitted)
                    throw Conflict("The remote recovery result is not durable yet.");
                EnsureDirectory(connection, record.Request.PriorStreamId, requireOwnerStream: true);
                var updatedDirectory = connection.Execute("UPDATE gp_account_principal_directory SET client_stream_id=? WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND platform_user_id=? AND installation_id=? AND client_stream_id=?",
                    record.Request.SuccessorStreamId.ToString(), scope.BackendNamespace.Value, owner.AppId.ToString(), owner.Principal.Issuer, owner.Principal.Subject,
                    owner.AccountId!.Value.ToString(), owner.InstallationId.ToString("D"), record.Request.PriorStreamId.ToString());
                if (updatedDirectory != 1)
                    throw Conflict("The issued account directory changed before successor adoption.");
                var updatedJournal = connection.Execute("UPDATE gp_command_stream_recovery SET phase=3 WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND recovery_id=? AND phase=2",
                    scope.BackendNamespace.Value, owner.AppId.ToString(), owner.Principal.Issuer, owner.Principal.Subject, recoveryId.ToString("D"));
                if (updatedJournal != 1)
                    throw Conflict("The recovery journal changed during successor adoption.");
                return ReadRecord(connection, recoveryId, true)!;
            }), cancellationToken);

        public Task<CommandStreamRecoveryRecord> MarkBootstrapCompleteAsync(Guid recoveryId, CancellationToken cancellationToken) =>
            database.ExecuteAsync(scope, connection => InCallerTransaction(connection, cancellationToken, () =>
            {
                var record = RequireRecord(connection, recoveryId);
                EnsureDirectory(connection, record.Request.SuccessorStreamId, requireOwnerStream: true);
                if (record.Phase == CommandStreamRecoveryPhase.BootstrapComplete)
                    return record;
                if (record.Phase != CommandStreamRecoveryPhase.Adopted)
                    throw Conflict("The successor has not been adopted.");
                var updated = connection.Execute("UPDATE gp_command_stream_recovery SET phase=4 WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND recovery_id=? AND phase=3",
                    scope.BackendNamespace.Value, owner.AppId.ToString(), owner.Principal.Issuer, owner.Principal.Subject, recoveryId.ToString("D"));
                if (updated != 1)
                    throw Conflict("The recovery journal changed while completing bootstrap.");
                return ReadRecord(connection, recoveryId, true)!;
            }), cancellationToken);

        private CommandStreamRecoveryRecord? Load(SQLiteConnection connection, bool checkDirectory)
        {
            var record = ReadLatest(connection);
            if (record == null)
            {
                if (checkDirectory)
                    EnsureDirectory(connection, owner.StreamId, requireOwnerStream: true);
                return null;
            }
            if (checkDirectory)
            {
                var expected = record.Phase >= CommandStreamRecoveryPhase.Adopted ? record.Request.SuccessorStreamId : record.Request.PriorStreamId;
                EnsureDirectory(connection, expected, requireOwnerStream: true);
            }
            return record;
        }

        private CommandStreamRecoveryRecord? ReadRecord(SQLiteConnection connection, Guid recoveryId, bool checkDirectory)
        {
            var rows = connection.Query<JournalRow>("SELECT platform_user_id AS AccountId,installation_id AS InstallationId,prior_stream_id AS PriorStreamId,successor_stream_id AS SuccessorStreamId,recovery_id AS RecoveryId,reason AS Reason,inspection_token AS InspectionToken,expected_finalized_through AS ExpectedFinalizedThrough,phase AS Phase,remote_next_sequence AS RemoteNextSequence,remote_prior_finalized_through AS RemotePriorFinalizedThrough FROM gp_command_stream_recovery WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND recovery_id=?",
                scope.BackendNamespace.Value, owner.AppId.ToString(), owner.Principal.Issuer, owner.Principal.Subject, recoveryId.ToString("D"));
            if (rows.Count == 0)
                return null;
            if (rows.Count != 1)
                throw new StorageException(StorageFailure.Corrupt, "The recovery journal contains duplicate recovery records.");
            var row = rows[0];
            try
            {
                if (new PlatformUserId(Guid.Parse(row.AccountId)) != owner.AccountId || Guid.Parse(row.InstallationId) != owner.InstallationId)
                    throw new StorageException(StorageFailure.InvalidOwner, "The recovery journal belongs to another issued account or installation.");
                var request = new CommandStreamRecoveryRequest(owner.Principal, owner.AppId, owner.AccountId!.Value, owner.InstallationId,
                    new ClientStreamId(Guid.Parse(row.PriorStreamId)), new ClientStreamId(Guid.Parse(row.SuccessorStreamId)), Guid.Parse(row.RecoveryId),
                    row.Reason, row.InspectionToken, row.ExpectedFinalizedThrough);
                if (row.Phase < 0 || row.Phase > 4)
                    throw new StorageException(StorageFailure.Corrupt, "The recovery journal phase is invalid.");
                var phase = (CommandStreamRecoveryPhase)row.Phase;
                if ((phase <= CommandStreamRecoveryPhase.Prepared && (row.RemoteNextSequence.HasValue || row.RemotePriorFinalizedThrough.HasValue)) ||
                    (phase >= CommandStreamRecoveryPhase.RemoteCommitted && (row.RemoteNextSequence != 1 || row.RemotePriorFinalizedThrough != request.ExpectedFinalizedThrough)))
                    throw new StorageException(StorageFailure.Corrupt, "The recovery journal remote receipt is inconsistent.");
                var result = new CommandStreamRecoveryRecord(request, phase, row.RemoteNextSequence, row.RemotePriorFinalizedThrough);
                if (checkDirectory)
                {
                    var expected = phase >= CommandStreamRecoveryPhase.Adopted ? request.SuccessorStreamId : request.PriorStreamId;
                    EnsureDirectory(connection, expected, true);
                }
                return result;
            }
            catch (StorageException) { throw; }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            {
                throw new StorageException(StorageFailure.Corrupt, "The recovery journal identity is invalid.", error);
            }
        }

        private CommandStreamRecoveryRecord? ReadLatest(SQLiteConnection connection)
        {
            var currentStream = connection.ExecuteScalar<string>("SELECT client_stream_id FROM gp_account_principal_directory WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND platform_user_id=? AND installation_id=?",
                scope.BackendNamespace.Value, owner.AppId.ToString(), owner.Principal.Issuer, owner.Principal.Subject, owner.AccountId!.Value.ToString(), owner.InstallationId.ToString("D"));
            if (string.IsNullOrEmpty(currentStream))
                return null;
            var recoveryId = connection.ExecuteScalar<string>("SELECT recovery_id FROM gp_command_stream_recovery WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND (phase<4 OR successor_stream_id=?) ORDER BY rowid DESC LIMIT 1",
                scope.BackendNamespace.Value, owner.AppId.ToString(), owner.Principal.Issuer, owner.Principal.Subject, currentStream);
            return string.IsNullOrEmpty(recoveryId) ? null : ReadRecord(connection, Guid.Parse(recoveryId), false);
        }

        private CommandStreamRecoveryRecord RequireRecord(SQLiteConnection connection, Guid recoveryId)
        {
            if (recoveryId == Guid.Empty || !UuidIdentity.IsValid(recoveryId, false))
                throw new ArgumentException("A UUIDv7 recovery ID is required.", nameof(recoveryId));
            var record = ReadRecord(connection, recoveryId, true);
            if (record == null || record.Request.RecoveryId != recoveryId)
                throw Conflict("The recovery request does not match the durable journal.");
            return record;
        }

        private void EnsureDirectory(SQLiteConnection connection, ClientStreamId expectedStream, bool requireOwnerStream)
        {
            var rows = connection.Query<DirectoryRow>("SELECT installation_id AS InstallationId,client_stream_id AS StreamId,platform_user_id AS AccountId,membership_status AS MembershipStatus FROM gp_account_principal_directory WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=?",
                scope.BackendNamespace.Value, owner.AppId.ToString(), owner.Principal.Issuer, owner.Principal.Subject);
            if (rows.Count != 1 || Guid.Parse(rows[0].InstallationId) != owner.InstallationId ||
                rows[0].AccountId == null || Guid.Parse(rows[0].AccountId) != owner.AccountId!.Value.Value ||
                !string.Equals(rows[0].MembershipStatus, "active", StringComparison.Ordinal) ||
                (requireOwnerStream && new ClientStreamId(Guid.Parse(rows[0].StreamId)) != expectedStream))
                throw new StorageException(StorageFailure.IdentityConflict, "The issued account directory no longer matches the recovery owner.");
        }

        private void ValidateRequestOwner(CommandStreamRecoveryRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (!request.Principal.Equals(owner.Principal) || request.AppId != owner.AppId || request.AccountId != owner.AccountId ||
                request.InstallationId != owner.InstallationId)
                throw new StorageException(StorageFailure.InvalidOwner, "The recovery request does not belong to the issued account directory entry.");
        }

        private static void ValidateRemoteResult(CommandStreamRecoveryRequest request, ClientStreamId prior, ClientStreamId successor, long watermark, long nextSequence)
        {
            if (prior != request.PriorStreamId || successor != request.SuccessorStreamId || watermark != request.ExpectedFinalizedThrough || nextSequence != 1)
                throw Conflict("The server recovery result does not match the immutable request.");
        }

        private static bool SameRequest(CommandStreamRecoveryRequest a, CommandStreamRecoveryRequest b) =>
            a.Principal.Equals(b.Principal) && a.AppId == b.AppId && a.AccountId == b.AccountId && a.InstallationId == b.InstallationId &&
            a.PriorStreamId == b.PriorStreamId && a.SuccessorStreamId == b.SuccessorStreamId && a.RecoveryId == b.RecoveryId &&
            string.Equals(a.Reason, b.Reason, StringComparison.Ordinal) && string.Equals(a.InspectionToken, b.InspectionToken, StringComparison.Ordinal) &&
            a.ExpectedFinalizedThrough == b.ExpectedFinalizedThrough;

        private static T InCallerTransaction<T>(SQLiteConnection connection, CancellationToken cancellationToken, Func<T> action)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = action();
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }

        private static StorageException Conflict(string message) => new StorageException(StorageFailure.IdentityConflict, message);

        private sealed class JournalRow
        {
            public string AccountId { get; set; } = string.Empty; public string InstallationId { get; set; } = string.Empty;
            public string PriorStreamId { get; set; } = string.Empty; public string SuccessorStreamId { get; set; } = string.Empty;
            public string RecoveryId { get; set; } = string.Empty; public string Reason { get; set; } = string.Empty;
            public string InspectionToken { get; set; } = string.Empty; public long ExpectedFinalizedThrough
            {
                get; set;
            }
            public int Phase
            {
                get; set;
            }
            public long? RemoteNextSequence
            {
                get; set;
            }
            public long? RemotePriorFinalizedThrough
            {
                get; set;
            }
        }
        private sealed class DirectoryRow
        {
            public string InstallationId { get; set; } = string.Empty; public string StreamId { get; set; } = string.Empty; public string? AccountId
            {
                get; set;
            }
            public string MembershipStatus { get; set; } = string.Empty;
        }
    }
}
