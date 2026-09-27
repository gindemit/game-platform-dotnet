using GamePlatform.Storage.Sqlite.Migrations;

namespace GamePlatform.Storage.Sqlite.Features.Accounts
{
    /// <summary>Initial immutable schema for the account-neutral principal directory database.</summary>
    public static class SqlitePrincipalDirectoryMigration
    {
        public static SqliteMigration Create() => new SqliteMigration(1, "principal-directory-v1", new[]
        {
            "CREATE TABLE gp_account_principal_directory (backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL, issuer TEXT NOT NULL, subject TEXT NOT NULL, installation_id TEXT NOT NULL, client_stream_id TEXT NOT NULL, platform_user_id TEXT NULL, membership_status TEXT NULL, PRIMARY KEY(backend_namespace, app_id, issuer, subject), UNIQUE(backend_namespace, app_id, installation_id), UNIQUE(backend_namespace, app_id, client_stream_id), CHECK((platform_user_id IS NULL AND membership_status IS NULL) OR (platform_user_id IS NOT NULL AND membership_status IS NOT NULL)))",
            "CREATE UNIQUE INDEX gp_account_principal_directory_issued_account ON gp_account_principal_directory(backend_namespace, app_id, platform_user_id) WHERE platform_user_id IS NOT NULL"
        });
    }

    /// <summary>Immutable platform migration v4 for the portable Accounts directory.</summary>
    public static class SqliteAccountsDirectoryMigration
    {
        public static SqliteMigration Create(int version) => new SqliteMigration(version, "accounts-directory-v1", new[]
        {
            "CREATE TABLE gp_accounts_directory (backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL, issuer TEXT NOT NULL, subject TEXT NOT NULL, installation_id TEXT NOT NULL, client_stream_id TEXT NOT NULL, platform_user_id TEXT NULL, membership_status TEXT NULL, PRIMARY KEY(backend_namespace, app_id, issuer, subject), UNIQUE(backend_namespace, app_id, installation_id), UNIQUE(backend_namespace, app_id, client_stream_id), CHECK((platform_user_id IS NULL AND membership_status IS NULL) OR (platform_user_id IS NOT NULL AND membership_status IS NOT NULL)))",
            "CREATE UNIQUE INDEX gp_accounts_directory_issued_account ON gp_accounts_directory(backend_namespace, app_id, platform_user_id) WHERE platform_user_id IS NOT NULL"
        });
    }
}
