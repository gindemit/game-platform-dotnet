using GamePlatform.Storage.Sqlite.Migrations;

namespace GamePlatform.Storage.Sqlite.Outbox
{
    /// <summary>
    /// Immutable platform migration v6. Existing retained outbox rows cannot
    /// honestly acquire their original installation and creation timestamp, so
    /// this migration deliberately refuses them rather than synthesizing data.
    /// </summary>
    public static class SqliteEnvelopeAndZeroRevisionMigration
    {
        public static SqliteMigration Create(int version) => new SqliteMigration(
            version,
            "platform-command-envelope-and-zero-revisions-v1",
            new[]
            {
                "CREATE TABLE gp_stream_state_v6 (singleton INTEGER PRIMARY KEY CHECK(singleton = 1), backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL, account_id TEXT NOT NULL, client_stream_id TEXT NOT NULL, installation_id TEXT NOT NULL, ready INTEGER NOT NULL CHECK(ready IN (0, 1)), next_sequence INTEGER NOT NULL CHECK(next_sequence > 0), local_revision INTEGER NOT NULL CHECK(local_revision >= 0), finalized_through INTEGER NOT NULL CHECK(finalized_through >= 0))",
                "INSERT INTO gp_stream_state_v6(singleton, backend_namespace, app_id, account_id, client_stream_id, installation_id, ready, next_sequence, local_revision, finalized_through) SELECT s.singleton, s.backend_namespace, s.app_id, s.account_id, s.client_stream_id, d.installation_id, s.ready, s.next_sequence, s.local_revision, s.finalized_through FROM gp_stream_state s JOIN gp_accounts_directory d ON d.backend_namespace = s.backend_namespace AND d.app_id = s.app_id AND d.platform_user_id = s.account_id AND d.client_stream_id = s.client_stream_id",
                "CREATE TABLE gp_stream_state_v6_guard (retained_row_count INTEGER NOT NULL CHECK(retained_row_count = 0))",
                "INSERT INTO gp_stream_state_v6_guard(retained_row_count) SELECT (SELECT COUNT(*) FROM gp_stream_state) - (SELECT COUNT(*) FROM gp_stream_state_v6)",
                "DROP TABLE gp_stream_state",
                "ALTER TABLE gp_stream_state_v6 RENAME TO gp_stream_state",
                "DROP TABLE gp_stream_state_v6_guard",
                "CREATE TABLE gp_outbox_v6 (operation_id TEXT PRIMARY KEY, backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL, account_id TEXT NOT NULL, client_stream_id TEXT NOT NULL, installation_id TEXT NOT NULL, sequence INTEGER NOT NULL CHECK(sequence > 0), business_run_id TEXT NOT NULL UNIQUE, operation_kind TEXT NOT NULL, schema_version INTEGER NOT NULL CHECK(schema_version > 0), fingerprint_version INTEGER NOT NULL CHECK(fingerprint_version > 0), client_created_at INTEGER NOT NULL CHECK(client_created_at >= 0 AND client_created_at <= 253402300799999), semantic_body BLOB NOT NULL, fingerprint BLOB NOT NULL, local_revision INTEGER NOT NULL CHECK(local_revision > 0), delivery_state TEXT NOT NULL DEFAULT 'pending' CHECK(delivery_state IN ('pending', 'in_flight', 'accepted', 'terminal_rejected')), leased_until INTEGER NULL, terminal_result BLOB NULL, UNIQUE(client_stream_id, sequence))",
                "CREATE TABLE gp_outbox_v6_guard (retained_row_count INTEGER NOT NULL CHECK(retained_row_count = 0))",
                "INSERT INTO gp_outbox_v6_guard(retained_row_count) SELECT COUNT(*) FROM gp_outbox",
                "DROP TRIGGER gp_outbox_immutable_update",
                "DROP TRIGGER gp_outbox_no_delete",
                "DROP TABLE gp_outbox",
                "ALTER TABLE gp_outbox_v6 RENAME TO gp_outbox",
                "DROP TABLE gp_outbox_v6_guard",
                "CREATE TRIGGER gp_outbox_immutable_update BEFORE UPDATE OF operation_id, backend_namespace, app_id, account_id, client_stream_id, installation_id, sequence, business_run_id, operation_kind, schema_version, fingerprint_version, client_created_at, semantic_body, fingerprint, local_revision ON gp_outbox BEGIN SELECT RAISE(ABORT, 'immutable outbox identity'); END",
                "CREATE TRIGGER gp_outbox_no_delete BEFORE DELETE ON gp_outbox BEGIN SELECT RAISE(ABORT, 'outbox rows are retained'); END",
                "CREATE TABLE gp_feature_state_v6 (view_key TEXT NOT NULL, feature_namespace TEXT NOT NULL, entity_key TEXT NOT NULL, revision INTEGER NOT NULL CHECK(revision >= 0), confirmed_at INTEGER NOT NULL CHECK(confirmed_at >= 0), payload BLOB NOT NULL CHECK(length(payload) <= 262144), extensions BLOB NOT NULL CHECK(length(extensions) <= 65536), PRIMARY KEY(view_key,feature_namespace,entity_key))",
                "INSERT INTO gp_feature_state_v6(view_key, feature_namespace, entity_key, revision, confirmed_at, payload, extensions) SELECT view_key, feature_namespace, entity_key, revision, confirmed_at, payload, extensions FROM gp_feature_state",
                "DROP TABLE gp_feature_state",
                "ALTER TABLE gp_feature_state_v6 RENAME TO gp_feature_state",
                "CREATE TABLE gp_confirmed_projection_v6 (collection TEXT NOT NULL, entity_key TEXT NOT NULL, entity_revision INTEGER NOT NULL CHECK(entity_revision >= 0), state TEXT NOT NULL CHECK(state IN ('visible','removed','tombstone')), payload BLOB NULL, PRIMARY KEY(collection,entity_key))",
                "INSERT INTO gp_confirmed_projection_v6(collection, entity_key, entity_revision, state, payload) SELECT collection, entity_key, entity_revision, state, payload FROM gp_confirmed_projection",
                "DROP TABLE gp_confirmed_projection",
                "ALTER TABLE gp_confirmed_projection_v6 RENAME TO gp_confirmed_projection"
            });
    }
}
