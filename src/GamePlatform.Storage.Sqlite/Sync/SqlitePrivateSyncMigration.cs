using GamePlatform.Storage.Sqlite.Migrations;

namespace GamePlatform.Storage.Sqlite.Sync
{
    public static class SqlitePrivateSyncMigration
    {
        public static SqliteMigration Create(int version) => new SqliteMigration(version,"platform-private-sync-v1",new[]
        {
            "CREATE TABLE gp_sync_state (singleton INTEGER PRIMARY KEY CHECK(singleton = 1), client_stream_id TEXT NOT NULL, ready INTEGER NOT NULL CHECK(ready IN (0,1)), pull_cursor BLOB NULL, committed_through INTEGER NOT NULL CHECK(committed_through >= 0), fixed_through INTEGER NULL CHECK(fixed_through >= 0), visibility_generation INTEGER NOT NULL CHECK(visibility_generation >= 0), log_epoch TEXT NULL)",
            "CREATE TABLE gp_confirmed_projection (collection TEXT NOT NULL, entity_key TEXT NOT NULL, entity_revision INTEGER NOT NULL CHECK(entity_revision > 0), state TEXT NOT NULL CHECK(state IN ('visible','removed','tombstone')), payload BLOB NULL, PRIMARY KEY(collection,entity_key))",
            "CREATE TABLE gp_bootstrap_state (singleton INTEGER PRIMARY KEY CHECK(singleton = 1), client_stream_id TEXT NOT NULL, session_token BLOB NOT NULL, next_page_token BLOB NOT NULL, committed_through INTEGER NOT NULL, visibility_generation INTEGER NOT NULL, log_epoch TEXT NOT NULL, expires_at INTEGER NOT NULL, manifest TEXT NOT NULL)",
            "CREATE TABLE gp_bootstrap_entities (collection TEXT NOT NULL, entity_key TEXT NOT NULL, entity_revision INTEGER NOT NULL, payload BLOB NOT NULL, PRIMARY KEY(collection,entity_key))",
            "CREATE TABLE gp_bootstrap_pages (page_token BLOB PRIMARY KEY, next_page_token BLOB NULL, final_cursor BLOB NULL)"
        });
    }
}
