using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Accounts;
using SQLite;

namespace GamePlatform.Storage.Sqlite.Features.Accounts
{

    /// <summary>Durable account directory; its database owner is only backend plus app.</summary>
    public sealed class SqliteAccountsDirectoryStore : IAccountDirectoryStore
    {
        private readonly SqliteAccountsDirectoryDatabase database;
        private readonly AccountDirectoryScope scope;
        private readonly Action<SqliteAccountsDirectoryExtensionTransaction, AccountDirectoryEntry>? onNewReservation;

        public SqliteAccountsDirectoryStore(SqliteAccountsDirectoryDatabase database, AccountDirectoryScope scope,
            Action<SqliteAccountsDirectoryExtensionTransaction, AccountDirectoryEntry>? onNewReservation = null)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            if (!scope.IsValid)
                throw new ArgumentException("A valid account directory scope is required.", nameof(scope));
            if (!database.Scope.Equals(scope))
                throw new StorageException(StorageFailure.InvalidOwner, "The account directory scope does not match its database.");
            this.scope = scope;
            this.onNewReservation = onNewReservation;
        }

        public Task<bool> HasAnyEntryAsync(CancellationToken cancellationToken) =>
            database.ExecuteAsync(scope, connection =>
            {
                var total = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_account_principal_directory");
                var inScope = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_account_principal_directory WHERE backend_namespace=? AND app_id=?", scope.BackendNamespace.Value, scope.AppId.ToString());
                if (total != inScope)
                    throw new StorageException(StorageFailure.InvalidOwner, "The principal directory contains an entry outside its backend/app scope.");
                return inScope != 0;
            }, cancellationToken);

        public Task<AccountDirectoryEntry?> FindAsync(AccountPrincipalDescriptor principal, AppId appId, CancellationToken cancellationToken)
        {
            Validate(principal, appId);
            return database.ExecuteAsync(scope, connection => Read(connection, principal, appId), cancellationToken);
        }

        public Task<AccountDirectoryEntry> ReserveAsync(AccountPrincipalDescriptor principal, AppId appId, Guid installationId, ClientStreamId streamId, CancellationToken cancellationToken)
        {
            Validate(principal, appId);
            if (installationId == Guid.Empty || !UuidIdentity.IsValid(installationId, false) || !streamId.IsValid)
                throw new ArgumentException("New installation and stream identifiers must be UUIDv7.");
            return database.ExecuteAsync(scope, connection =>
            {
                var existing = Read(connection, principal, appId);
                if (existing != null)
                    return existing;
                connection.Execute("INSERT INTO gp_account_principal_directory(backend_namespace,app_id,issuer,subject,installation_id,client_stream_id,platform_user_id,membership_status) VALUES (?,?,?,?,?,?,NULL,NULL)", principal.BackendNamespace.Value, appId.ToString(), principal.Issuer, principal.Subject, installationId.ToString("D"), streamId.ToString());
                var reserved = new AccountDirectoryEntry(principal, appId, installationId, streamId, null, null);
                if (onNewReservation != null)
                {
                    var borrowed = new SqliteAccountsDirectoryExtensionTransaction(connection);
                    try
                    {
                        onNewReservation(borrowed, reserved);
                    }
                    finally { borrowed.Invalidate(); }
                }
                return reserved;
            }, cancellationToken);
        }

        public Task<AccountDirectoryEntry> BindIssuedAccountAsync(AccountDirectoryEntry reservation, PlatformUserId accountId, string membershipStatus, CancellationToken cancellationToken)
        {
            if (reservation == null)
                throw new ArgumentNullException(nameof(reservation));
            Validate(reservation.Principal, reservation.AppId);
            if (!accountId.IsValid || string.IsNullOrWhiteSpace(membershipStatus) || membershipStatus.Length > 64)
                throw new ArgumentException("A valid issued account and membership are required.");
            return database.ExecuteAsync(scope, connection =>
            {
                var current = Read(connection, reservation.Principal, reservation.AppId);
                if (current == null || current.InstallationId != reservation.InstallationId || current.StreamId != reservation.StreamId)
                    throw new StorageException(StorageFailure.IdentityConflict, "The durable reservation changed.");
                if (current.HasIssuedAccount)
                {
                    if (current.AccountId != accountId || !string.Equals(current.MembershipStatus, membershipStatus, StringComparison.Ordinal))
                        throw new StorageException(StorageFailure.IdentityConflict, "The issued account mapping changed.");
                    return current;
                }
                var updated = connection.Execute("UPDATE gp_account_principal_directory SET platform_user_id=?,membership_status=? WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND platform_user_id IS NULL", accountId.ToString(), membershipStatus, reservation.Principal.BackendNamespace.Value, reservation.AppId.ToString(), reservation.Principal.Issuer, reservation.Principal.Subject);
                if (updated != 1)
                    throw new StorageException(StorageFailure.IdentityConflict, "The durable reservation could not be bound exactly once.");
                return new AccountDirectoryEntry(reservation.Principal, reservation.AppId, reservation.InstallationId, reservation.StreamId, accountId, membershipStatus);
            }, cancellationToken);
        }

        private static AccountDirectoryEntry? Read(SQLiteConnection connection, AccountPrincipalDescriptor principal, AppId appId)
        {
            var rows = connection.Query<DirectoryRow>("SELECT installation_id AS InstallationId,client_stream_id AS ClientStreamId,platform_user_id AS PlatformUserId,membership_status AS MembershipStatus FROM gp_account_principal_directory WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=?", principal.BackendNamespace.Value, appId.ToString(), principal.Issuer, principal.Subject);
            if (rows.Count == 0)
                return null;
            if (rows.Count != 1)
                throw new StorageException(StorageFailure.Corrupt, "The account directory contains duplicate principal entries.");
            var row = rows[0];
            try
            {
                var installation = Guid.Parse(row.InstallationId);
                var stream = new ClientStreamId(Guid.Parse(row.ClientStreamId));
                return row.PlatformUserId == null
                    ? new AccountDirectoryEntry(principal, appId, installation, stream, null, null)
                    : new AccountDirectoryEntry(principal, appId, installation, stream, new PlatformUserId(Guid.Parse(row.PlatformUserId)), row.MembershipStatus);
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            {
                throw new StorageException(StorageFailure.Corrupt, "The account directory contains an invalid identity record.", error);
            }
        }

        private void Validate(AccountPrincipalDescriptor principal, AppId appId)
        {
            if (principal == null)
                throw new ArgumentNullException(nameof(principal));
            if (!appId.IsValid)
                throw new ArgumentException("A valid app is required.", nameof(appId));
            if (principal.BackendNamespace != scope.BackendNamespace || appId != scope.AppId)
                throw new StorageException(StorageFailure.InvalidOwner, "The principal or app does not belong to this directory scope.");
        }

        private sealed class DirectoryRow
        {
            public string InstallationId { get; set; } = string.Empty;
            public string ClientStreamId { get; set; } = string.Empty;
            public string? PlatformUserId { get; set; }
            public string? MembershipStatus { get; set; }
        }
    }
}
