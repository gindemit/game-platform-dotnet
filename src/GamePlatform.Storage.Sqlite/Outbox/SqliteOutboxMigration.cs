using GamePlatform.Storage.Sqlite.Migrations;

namespace GamePlatform.Storage.Sqlite.Outbox
{
    public static class SqliteOutboxMigration
    {
        public static SqliteMigration Create(int version) => new SqliteMigration(
            version,
            "platform-outbox-v1",
            new[]
            {
                "CREATE TABLE gp_stream_state (singleton INTEGER PRIMARY KEY CHECK(singleton = 1), backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL, account_id TEXT NOT NULL, client_stream_id TEXT NOT NULL, ready INTEGER NOT NULL CHECK(ready IN (0, 1)), next_sequence INTEGER NOT NULL CHECK(next_sequence > 0), local_revision INTEGER NOT NULL CHECK(local_revision >= 0), finalized_through INTEGER NOT NULL CHECK(finalized_through >= 0))",
                "CREATE TABLE gp_outbox (operation_id TEXT PRIMARY KEY, backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL, account_id TEXT NOT NULL, client_stream_id TEXT NOT NULL, sequence INTEGER NOT NULL CHECK(sequence > 0), business_run_id TEXT NOT NULL UNIQUE, operation_kind TEXT NOT NULL, schema_version INTEGER NOT NULL CHECK(schema_version > 0), fingerprint_version INTEGER NOT NULL CHECK(fingerprint_version > 0), semantic_body BLOB NOT NULL, fingerprint BLOB NOT NULL, local_revision INTEGER NOT NULL CHECK(local_revision > 0), delivery_state TEXT NOT NULL DEFAULT 'pending' CHECK(delivery_state IN ('pending', 'in_flight', 'accepted', 'terminal_rejected')), leased_until INTEGER NULL, terminal_result BLOB NULL, UNIQUE(client_stream_id, sequence))",
                "CREATE TRIGGER gp_outbox_immutable_update BEFORE UPDATE OF operation_id, backend_namespace, app_id, account_id, client_stream_id, sequence, business_run_id, operation_kind, schema_version, fingerprint_version, semantic_body, fingerprint, local_revision ON gp_outbox BEGIN SELECT RAISE(ABORT, 'immutable outbox identity'); END",
                "CREATE TRIGGER gp_outbox_no_delete BEFORE DELETE ON gp_outbox BEGIN SELECT RAISE(ABORT, 'outbox rows are retained'); END"
            });
    }
}
