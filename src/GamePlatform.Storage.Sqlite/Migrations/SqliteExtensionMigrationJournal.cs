namespace GamePlatform.Storage.Sqlite.Migrations
{
    /// <summary>
    /// Platform-owned journal for explicitly supplied consumer extension migrations.
    /// This is platform migration v5; consumers never create or alter gp_* tables.
    /// </summary>
    public static class SqliteExtensionMigrationJournal
    {
        public static SqliteMigration Create(int version) => new SqliteMigration(version, "platform-extension-migrations-v1", new[]
        {
            "CREATE TABLE gp_extension_migrations (extension_namespace TEXT NOT NULL, version INTEGER NOT NULL CHECK(version > 0), migration_id TEXT NOT NULL, checksum TEXT NOT NULL, minimum_platform_version INTEGER NOT NULL CHECK(minimum_platform_version > 0), backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL, account_id TEXT NOT NULL, applied_at INTEGER NOT NULL, PRIMARY KEY(extension_namespace, version), UNIQUE(extension_namespace, migration_id))"
        });
    }
}
