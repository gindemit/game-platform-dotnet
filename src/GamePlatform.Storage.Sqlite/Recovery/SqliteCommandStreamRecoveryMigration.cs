using System;
using SQLite;
using GamePlatform.Storage.Abstractions;

namespace GamePlatform.Storage.Sqlite.Recovery
{
    internal static class SqliteCommandStreamRecoveryMigration
    {
        private const string Id = "command-stream-recovery-journal-v1";
        private const int Version = 1;
        private const string MarkerSql = "CREATE TABLE gp_command_stream_recovery_schema (singleton INTEGER PRIMARY KEY CHECK(singleton=1), version INTEGER NOT NULL, migration_id TEXT NOT NULL, backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL)";
        private const string TableSql = "CREATE TABLE gp_command_stream_recovery (backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL, issuer TEXT NOT NULL, subject TEXT NOT NULL, platform_user_id TEXT NOT NULL, installation_id TEXT NOT NULL, prior_stream_id TEXT NOT NULL, successor_stream_id TEXT NOT NULL, recovery_id TEXT NOT NULL, reason TEXT NOT NULL, inspection_token TEXT NOT NULL, expected_finalized_through INTEGER NOT NULL CHECK(expected_finalized_through >= 0), phase INTEGER NOT NULL CHECK(phase BETWEEN 1 AND 4), remote_next_sequence INTEGER NULL, remote_prior_finalized_through INTEGER NULL, PRIMARY KEY(backend_namespace, app_id, issuer, subject, recovery_id), CHECK((phase = 1 AND remote_next_sequence IS NULL AND remote_prior_finalized_through IS NULL) OR (phase >= 2 AND remote_next_sequence = 1 AND remote_prior_finalized_through = expected_finalized_through)))";

        internal static void Apply(SQLiteConnection connection, string backendNamespace, string appId)
        {
            connection.Execute("CREATE TABLE IF NOT EXISTS gp_command_stream_recovery_schema (singleton INTEGER PRIMARY KEY CHECK(singleton=1), version INTEGER NOT NULL, migration_id TEXT NOT NULL, backend_namespace TEXT NOT NULL, app_id TEXT NOT NULL)");
            if (!string.Equals(connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE type='table' AND name='gp_command_stream_recovery_schema'"), MarkerSql, StringComparison.Ordinal))
                throw new StorageException(StorageFailure.Migration, "The command recovery journal schema marker changed.");
            var count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_command_stream_recovery_schema");
            if (count == 0)
            {
                connection.Execute(TableSql);
                connection.Execute("INSERT INTO gp_command_stream_recovery_schema(singleton,version,migration_id,backend_namespace,app_id) VALUES (1,?,?,?,?)", Version, Id, backendNamespace, appId);
            }
            else if (count != 1 || connection.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_command_stream_recovery_schema WHERE singleton=1 AND ((version=? AND migration_id=?) OR (version=2 AND migration_id='command-stream-recovery-journal-v2')) AND backend_namespace=? AND app_id=?", Version, Id, backendNamespace, appId) != 1)
                throw new StorageException(StorageFailure.Migration, "The command recovery journal schema marker is invalid.");
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='gp_command_stream_recovery'") != 1 ||
                !string.Equals(connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE type='table' AND name='gp_command_stream_recovery'"),
                    connection.ExecuteScalar<int>("SELECT version FROM gp_command_stream_recovery_schema") == 1 ? TableSql : TableSqlV2, StringComparison.Ordinal))
                throw new StorageException(StorageFailure.Migration, "The command recovery journal schema changed.");
            if (connection.ExecuteScalar<int>("SELECT version FROM gp_command_stream_recovery_schema") == 1)
            {
                // The directory caller owns this migration transaction. Preserve every immutable request.
                connection.Execute("ALTER TABLE gp_command_stream_recovery RENAME TO gp_command_stream_recovery_v1");
                connection.Execute(TableSqlV2);
                connection.Execute("INSERT INTO gp_command_stream_recovery SELECT * FROM gp_command_stream_recovery_v1 ORDER BY rowid");
                connection.Execute("DROP TABLE gp_command_stream_recovery_v1");
                connection.Execute("UPDATE gp_command_stream_recovery_schema SET version=2,migration_id=? WHERE singleton=1", "command-stream-recovery-journal-v2");
            }
        }

        private static readonly string TableSqlV2 = TableSql.Replace("phase BETWEEN 1 AND 4", "phase BETWEEN 0 AND 4").Replace("phase = 1 AND", "phase IN (0,1) AND");
    }
}
