using GamePlatform.Storage.Sqlite.Migrations;

namespace GamePlatform.Storage.Sqlite.Sync
{
    /// <summary>
    /// Immutable platform migration v7. The frozen private-feed invalidation
    /// state remains distinct from view removal and tombstones in durable state.
    /// </summary>
    public static class SqliteConfirmedProjectionInvalidationMigration
    {
        public static SqliteMigration Create(int version) => new SqliteMigration(
            version,
            "platform-confirmed-projection-invalidation-v1",
            new[]
            {
                "CREATE TABLE gp_confirmed_projection_v7 (collection TEXT NOT NULL, entity_key TEXT NOT NULL, entity_revision INTEGER NOT NULL CHECK(entity_revision >= 0), state TEXT NOT NULL CHECK(state IN ('visible','removed','tombstone','invalidation')), payload BLOB NULL, PRIMARY KEY(collection,entity_key))",
                "INSERT INTO gp_confirmed_projection_v7(collection, entity_key, entity_revision, state, payload) SELECT collection, entity_key, entity_revision, state, payload FROM gp_confirmed_projection",
                "DROP TABLE gp_confirmed_projection",
                "ALTER TABLE gp_confirmed_projection_v7 RENAME TO gp_confirmed_projection"
            });
    }
}
