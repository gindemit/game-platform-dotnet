#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;

namespace GamePlatform.Tests.Sqlite
{
    public sealed class Cl010MigrationRegistryTests
    {
        private static readonly StorageScope Scope = new StorageScope(
            "test-backend",
            new PlatformId("01890f3e-7a6b-7c8d-9e0f-102030405060"),
            new PlatformId("00112233-4455-4677-8899-aabbccddeeff"));
        private const string Stream = "0199f9a0-1111-7777-8888-999999999999";
        private const string Operation = "0199f9a0-2222-7777-8888-999999999999";

        [Fact]
        public async Task FreshDatabaseInstallsTheCompleteChecksummedRegistry()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);

            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (
                    Migrations: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"),
                    Outbox: TableExists(session, "gp_outbox"),
                    Sync: TableExists(session, "gp_sync_state"),
                    Bootstrap: TableExists(session, "gp_bootstrap_state"));
            }, CancellationToken.None);

            Assert.Equal(2, observed.Migrations);
            Assert.Equal(1, observed.Outbox);
            Assert.Equal(1, observed.Sync);
            Assert.Equal(1, observed.Bootstrap);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task RetainedOutboxVersionUpgradesWithoutRewritingReadyOrCommands()
        {
            using var files = new TemporaryDatabase();
            var prior = new[] { SqliteOutboxMigration.Create(1) };
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, prior, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                session.Execute("INSERT INTO gp_stream_state VALUES (1,?,?,?,?,1,2,1,0)", Scope.BackendNamespace, Scope.AppId.Value, Scope.AccountId.Value, Stream);
                session.Execute("INSERT INTO gp_outbox(operation_id,backend_namespace,app_id,account_id,client_stream_id,sequence,business_run_id,operation_kind,schema_version,fingerprint_version,semantic_body,fingerprint,local_revision) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)", Operation, Scope.BackendNamespace, Scope.AppId.Value, Scope.AccountId.Value, Stream, 1L, "retained-run", "profile.patch", 1, 1, new byte[] { 1, 2 }, new byte[] { 3, 4 }, 1L);
                return true;
            }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (
                    Ready: session.ExecuteScalar<int>("SELECT ready FROM gp_stream_state WHERE singleton = 1"),
                    Body: session.ExecuteScalar<byte[]>("SELECT semantic_body FROM gp_outbox WHERE operation_id = ?", Operation),
                    Migrations: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"),
                    Sync: TableExists(session, "gp_sync_state"));
            }, CancellationToken.None);

            Assert.Equal(1, observed.Ready);
            Assert.Equal(new byte[] { 1, 2 }, observed.Body);
            Assert.Equal(2, observed.Migrations);
            Assert.Equal(1, observed.Sync);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ReopenIsIdempotentAndPreservesViewCheckpointAndStaging()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                session.Execute("INSERT INTO gp_sync_state VALUES (1,?,1,?,7,NULL,3,?)", Stream, new byte[] { 8 }, "0199f9a0-aaaa-7777-8888-999999999999");
                session.Execute("INSERT INTO gp_confirmed_projection VALUES ('profile','self',9,'visible',?)", new byte[] { 10 });
                session.Execute("INSERT INTO gp_bootstrap_state VALUES (1,?,'active',7,8,?,?,?,?,?,?,?)", Stream, new byte[] { 1 }, new byte[] { 2 }, 7L, 3L, "0199f9a0-bbbb-7777-8888-999999999999", 1000L, "profile");
                session.Execute("INSERT INTO gp_bootstrap_entities VALUES ('inventory','item:1',4,?)", new byte[] { 11 });
                session.Execute("INSERT INTO gp_bootstrap_pages VALUES (?,?,NULL)", new byte[] { 2 }, new byte[] { 3 });
                return true;
            }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (
                    Migrations: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"),
                    Cursor: session.ExecuteScalar<byte[]>("SELECT pull_cursor FROM gp_sync_state WHERE singleton = 1"),
                    View: session.ExecuteScalar<byte[]>("SELECT payload FROM gp_confirmed_projection WHERE entity_key = 'self'"),
                    Staged: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_bootstrap_entities") + session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_bootstrap_pages"));
            }, CancellationToken.None);

            Assert.Equal(2, observed.Migrations);
            Assert.Equal(new byte[] { 8 }, observed.Cursor);
            Assert.Equal(new byte[] { 10 }, observed.View);
            Assert.Equal(2, observed.Staged);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task AppliedPrivateSyncChecksumDriftIsRejected()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var current = SqlitePlatformMigrationRegistry.Migrations[1];
            var changedStatements = current.Statements.Concat(new[] { "SELECT 1" }).ToArray();
            var changed = new[]
            {
                SqlitePlatformMigrationRegistry.Migrations[0],
                new SqliteMigration(current.Version, current.Id, changedStatements)
            };

            var error = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, changed, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, error.Failure);
        }

        [Fact]
        public async Task FailureBetweenPrivateSyncEffectsAndMarkerRollsBackTheWholeUpgrade()
        {
            using var files = new TemporaryDatabase();
            var prior = new[] { SqliteOutboxMigration.Create(1) };
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, prior, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO gp_stream_state VALUES (1,?,?,?,?,1,1,0,0)", Scope.BackendNamespace, Scope.AppId.Value, Scope.AccountId.Value, Stream);
                return true;
            }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var failure = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(
                files.Path,
                Scope,
                SqlitePlatformMigrationRegistry.Migrations,
                SqliteDatabaseOptions.Default,
                (checkpoint, migration) =>
                {
                    if (migration.Version == 2 && checkpoint == MigrationCheckpoint.EffectsApplied) throw new InjectedFailure();
                },
                CancellationToken.None));
            Assert.IsType<InjectedFailure>(failure.InnerException);

            database = await SqliteDatabase.OpenAsync(files.Path, Scope, prior, CancellationToken.None);
            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (
                    Migrations: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"),
                    Ready: session.ExecuteScalar<int>("SELECT ready FROM gp_stream_state WHERE singleton = 1"),
                    Sync: TableExists(session, "gp_sync_state"));
            }, CancellationToken.None);
            Assert.Equal(1, observed.Migrations);
            Assert.Equal(1, observed.Ready);
            Assert.Equal(0, observed.Sync);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        private static int TableExists(SqliteTransactionSession session, string table) =>
            session.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?", table);

        private sealed class InjectedFailure : Exception { }

        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl010-migrations", Guid.NewGuid().ToString("N"));
            public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); }
            public string Path { get; }
            public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
        }
    }
}
