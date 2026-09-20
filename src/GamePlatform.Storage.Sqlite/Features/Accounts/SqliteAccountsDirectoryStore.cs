#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Sqlite.Executor;

namespace GamePlatform.Storage.Sqlite.Features.Accounts
{
    /// <summary>Non-secret account-directory storage. Hosts supply a dedicated principal-directory database scope.</summary>
    public sealed class SqliteAccountsDirectoryStore : IAccountDirectoryStore
    {
        private readonly SqliteDatabase database; private readonly StorageScope scope;
        public SqliteAccountsDirectoryStore(SqliteDatabase database, StorageScope scope)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database)); this.scope = scope;
            if (!database.Scope.Equals(scope)) throw new StorageException(StorageFailure.InvalidOwner, "The account directory scope does not match the database.");
        }

        public Task<AccountDirectoryEntry?> FindAsync(AccountPrincipalDescriptor principal, AppId appId, CancellationToken cancellationToken)
        {
            Validate(principal, appId);
            return database.ExecuteAsync(scope, transaction => Read((SqliteTransactionSession)transaction, principal, appId), cancellationToken);
        }

        public Task<AccountDirectoryEntry> ReserveAsync(AccountPrincipalDescriptor principal, AppId appId, Guid installationId, ClientStreamId streamId, CancellationToken cancellationToken)
        {
            Validate(principal, appId); if (installationId == Guid.Empty || !UuidIdentity.IsValid(installationId, false) || !streamId.IsValid) throw new ArgumentException("New installation and stream identifiers must be UUIDv7.");
            return database.ExecuteAsync(scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction; var existing = Read(session, principal, appId);
                if (existing != null) return existing;
                session.Execute("INSERT INTO gp_accounts_directory(backend_namespace,app_id,issuer,subject,installation_id,client_stream_id,platform_user_id,membership_status) VALUES (?,?,?,?,?,?,NULL,NULL)", principal.BackendNamespace.Value, appId.ToString(), principal.Issuer, principal.Subject, installationId.ToString("D"), streamId.ToString());
                return new AccountDirectoryEntry(principal, appId, installationId, streamId, null, null);
            }, cancellationToken);
        }

        public Task<AccountDirectoryEntry> BindIssuedAccountAsync(AccountDirectoryEntry reservation, PlatformUserId accountId, string membershipStatus, CancellationToken cancellationToken)
        {
            if (reservation == null) throw new ArgumentNullException(nameof(reservation)); Validate(reservation.Principal, reservation.AppId);
            if (!accountId.IsValid || string.IsNullOrWhiteSpace(membershipStatus) || membershipStatus.Length > 64) throw new ArgumentException("A valid issued account and membership are required.");
            return database.ExecuteAsync(scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction; var current = Read(session, reservation.Principal, reservation.AppId);
                if (current == null || current.InstallationId != reservation.InstallationId || current.StreamId != reservation.StreamId) throw new StorageException(StorageFailure.IdentityConflict, "The durable reservation changed.");
                if (current.HasIssuedAccount)
                {
                    if (current.AccountId != accountId || !string.Equals(current.MembershipStatus, membershipStatus, StringComparison.Ordinal)) throw new StorageException(StorageFailure.IdentityConflict, "The issued account mapping changed.");
                    return current;
                }
                session.Execute("UPDATE gp_accounts_directory SET platform_user_id=?,membership_status=? WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND platform_user_id IS NULL", accountId.ToString(), membershipStatus, reservation.Principal.BackendNamespace.Value, reservation.AppId.ToString(), reservation.Principal.Issuer, reservation.Principal.Subject);
                return new AccountDirectoryEntry(reservation.Principal, reservation.AppId, reservation.InstallationId, reservation.StreamId, accountId, membershipStatus);
            }, cancellationToken);
        }

        private static AccountDirectoryEntry? Read(SqliteTransactionSession session, AccountPrincipalDescriptor principal, AppId appId)
        {
            var count = session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_accounts_directory WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=?", principal.BackendNamespace.Value, appId.ToString(), principal.Issuer, principal.Subject);
            if (count == 0) return null;
            var installation = Guid.Parse(session.ExecuteScalar<string>("SELECT installation_id FROM gp_accounts_directory WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=?", principal.BackendNamespace.Value, appId.ToString(), principal.Issuer, principal.Subject));
            var stream = new ClientStreamId(Guid.Parse(session.ExecuteScalar<string>("SELECT client_stream_id FROM gp_accounts_directory WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=?", principal.BackendNamespace.Value, appId.ToString(), principal.Issuer, principal.Subject)));
            var issued = session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_accounts_directory WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=? AND platform_user_id IS NOT NULL", principal.BackendNamespace.Value, appId.ToString(), principal.Issuer, principal.Subject) == 1;
            return issued ? new AccountDirectoryEntry(principal, appId, installation, stream, new PlatformUserId(Guid.Parse(session.ExecuteScalar<string>("SELECT platform_user_id FROM gp_accounts_directory WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=?", principal.BackendNamespace.Value, appId.ToString(), principal.Issuer, principal.Subject))), session.ExecuteScalar<string>("SELECT membership_status FROM gp_accounts_directory WHERE backend_namespace=? AND app_id=? AND issuer=? AND subject=?", principal.BackendNamespace.Value, appId.ToString(), principal.Issuer, principal.Subject)) : new AccountDirectoryEntry(principal, appId, installation, stream, null, null);
        }

        private static void Validate(AccountPrincipalDescriptor principal, AppId appId)
        {
            if (principal == null) throw new ArgumentNullException(nameof(principal)); if (!appId.IsValid) throw new ArgumentException("A valid app is required.", nameof(appId));
        }
    }
}
