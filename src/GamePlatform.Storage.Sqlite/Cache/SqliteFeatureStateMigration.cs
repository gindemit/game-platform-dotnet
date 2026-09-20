using GamePlatform.Storage.Sqlite.Migrations;

namespace GamePlatform.Storage.Sqlite.Cache
{
    public static class SqliteFeatureStateMigration
    {
        public static SqliteMigration Create(int version) => new SqliteMigration(version, "platform-feature-state-v1", new[]
        {
            "CREATE TABLE gp_feature_state (view_key TEXT NOT NULL, feature_namespace TEXT NOT NULL, entity_key TEXT NOT NULL, revision INTEGER NOT NULL CHECK(revision > 0), confirmed_at INTEGER NOT NULL CHECK(confirmed_at >= 0), payload BLOB NOT NULL CHECK(length(payload) <= 262144), extensions BLOB NOT NULL CHECK(length(extensions) <= 65536), PRIMARY KEY(view_key,feature_namespace,entity_key))"
        });
    }
}
