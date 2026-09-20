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

            Assert.Equal(7, observed.Migrations);
            Assert.Equal(1, observed.Outbox);
            Assert.Equal(1, observed.Sync);
            Assert.Equal(1, observed.Bootstrap);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task RetainedOutboxVersionFailsClosedWithoutInventingEnvelopeFields()
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

            var failure = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None));
            Assert.Equal(StorageFailure.Constraint, failure.Failure);
            database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations.Take(5).ToArray(), CancellationToken.None);
            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (Body: session.ExecuteScalar<byte[]>("SELECT semantic_body FROM gp_outbox WHERE operation_id = ?", Operation), Migrations: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"));
            }, CancellationToken.None);
            Assert.Equal(new byte[] { 1, 2 }, observed.Body);
            Assert.Equal(5, observed.Migrations);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task EmptyOutboxUpgradeBindsStreamToExactAccountsDirectoryInstallation()
        {
            using var files = new TemporaryDatabase();
            var v5 = SqlitePlatformMigrationRegistry.Migrations.Take(5).ToArray();
            var installation = "0199f9a0-3333-7777-8888-999999999999";
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, v5, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                session.Execute("INSERT INTO gp_accounts_directory(backend_namespace,app_id,issuer,subject,installation_id,client_stream_id,platform_user_id,membership_status) VALUES (?,?,?,?,?,?,?,?)", Scope.BackendNamespace, Scope.AppId.Value, "test-issuer", "test-subject", installation, Stream, Scope.AccountId.Value, "active");
                session.Execute("INSERT INTO gp_stream_state VALUES (1,?,?,?,?,1,1,0,0)", Scope.BackendNamespace, Scope.AppId.Value, Scope.AccountId.Value, Stream);
                return true;
            }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (Installation: session.ExecuteScalar<string>("SELECT installation_id FROM gp_stream_state WHERE singleton=1"), Migrations: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"));
            }, CancellationToken.None);
            Assert.Equal(installation, observed.Installation);
            Assert.Equal(7, observed.Migrations);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task VersionSixStoresZeroAndSigned64MaximumProjectionRevisionsExactly()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                session.Execute("INSERT INTO gp_feature_state VALUES ('private','progression','confirmed',0,0,?,?)", new byte[] { 1 }, Array.Empty<byte>());
                session.Execute("INSERT INTO gp_confirmed_projection VALUES ('progression','self',0,'visible',?)", new byte[] { 2 });
                session.Execute("INSERT INTO gp_feature_state VALUES ('private','wallet','balance',?,0,?,?)", long.MaxValue, new byte[] { 3 }, Array.Empty<byte>());
                return true;
            }, CancellationToken.None);
            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (FeatureZero: session.ExecuteScalar<long>("SELECT revision FROM gp_feature_state WHERE feature_namespace='progression'"), ProjectionZero: session.ExecuteScalar<long>("SELECT entity_revision FROM gp_confirmed_projection WHERE collection='progression'"), FeatureMaximum: session.ExecuteScalar<long>("SELECT revision FROM gp_feature_state WHERE feature_namespace='wallet'"));
            }, CancellationToken.None);
            Assert.Equal(0, observed.FeatureZero);
            Assert.Equal(0, observed.ProjectionZero);
            Assert.Equal(long.MaxValue, observed.FeatureMaximum);
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

            Assert.Equal(7, observed.Migrations);
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
                new SqliteMigration(current.Version, current.Id, changedStatements),
                SqlitePlatformMigrationRegistry.Migrations[2],
                SqlitePlatformMigrationRegistry.Migrations[3],
                SqlitePlatformMigrationRegistry.Migrations[4],
                SqlitePlatformMigrationRegistry.Migrations[5],
                SqlitePlatformMigrationRegistry.Migrations[6]
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

            var v6Failure = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None));
            Assert.Equal(StorageFailure.Constraint, v6Failure.Failure);
        }

        [Fact]
        public async Task VersionSevenUpgradesVersionSixAndPersistsInvalidationDistinctly()
        {
            using var files = new TemporaryDatabase();
            var v6 = SqlitePlatformMigrationRegistry.Migrations.Take(6).ToArray();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, v6, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                session.Execute("INSERT INTO gp_confirmed_projection VALUES ('profile','visible',4,'visible',?)", new byte[] { 1 });
                session.Execute("INSERT INTO gp_confirmed_projection VALUES ('inventory','removed',5,'removed',NULL)");
                session.Execute("INSERT INTO gp_confirmed_projection VALUES ('wallet','tombstone',6,'tombstone',NULL)");
                return true;
            }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                session.Execute("INSERT INTO gp_confirmed_projection VALUES ('entitlements','invalidated',7,'invalidation',NULL)");
                return (
                    Migrations: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"),
                    Visible: session.ExecuteScalar<string>("SELECT state FROM gp_confirmed_projection WHERE collection = 'profile'"),
                    Removed: session.ExecuteScalar<string>("SELECT state FROM gp_confirmed_projection WHERE collection = 'inventory'"),
                    Tombstone: session.ExecuteScalar<string>("SELECT state FROM gp_confirmed_projection WHERE collection = 'wallet'"),
                    Invalidation: session.ExecuteScalar<string>("SELECT state FROM gp_confirmed_projection WHERE collection = 'entitlements'"));
            }, CancellationToken.None);

            Assert.Equal(7, observed.Migrations);
            Assert.Equal("visible", observed.Visible);
            Assert.Equal("removed", observed.Removed);
            Assert.Equal("tombstone", observed.Tombstone);
            Assert.Equal("invalidation", observed.Invalidation);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task VersionSevenEffectsAndMarkerRollbackKeepTheVersionSixConstraint()
        {
            using var files = new TemporaryDatabase();
            var v6 = SqlitePlatformMigrationRegistry.Migrations.Take(6).ToArray();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, v6, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO gp_confirmed_projection VALUES ('profile','self',8,'visible',?)", new byte[] { 12 });
                return true;
            }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            foreach (var failedCheckpoint in new[] { MigrationCheckpoint.EffectsApplied, MigrationCheckpoint.MarkerInserted })
            {
                var failure = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(
                    files.Path,
                    Scope,
                    SqlitePlatformMigrationRegistry.Migrations,
                    SqliteDatabaseOptions.Default,
                    (checkpoint, migration) =>
                    {
                        if (migration.Version == 7 && checkpoint == failedCheckpoint) throw new InjectedFailure();
                    },
                    CancellationToken.None));
                Assert.IsType<InjectedFailure>(failure.InnerException);

                database = await SqliteDatabase.OpenAsync(files.Path, Scope, v6, CancellationToken.None);
                var observed = await database.ExecuteAsync(Scope, transaction =>
                {
                    var session = (SqliteTransactionSession)transaction;
                    var invalidation = Assert.Throws<SQLite.SQLiteException>(() => session.Execute("INSERT INTO gp_confirmed_projection VALUES ('profile','invalidated',9,'invalidation',NULL)"));
                    return (Migrations: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"), Payload: session.ExecuteScalar<byte[]>("SELECT payload FROM gp_confirmed_projection WHERE collection = 'profile' AND entity_key = 'self'"), InvalidationRejected: invalidation.Message.Contains("CHECK constraint failed", StringComparison.Ordinal));
                }, CancellationToken.None);
                Assert.Equal(6, observed.Migrations);
                Assert.Equal(new byte[] { 12 }, observed.Payload);
                Assert.True(observed.InvalidationRejected);
                Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
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
