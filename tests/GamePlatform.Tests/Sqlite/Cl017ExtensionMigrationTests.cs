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

namespace GamePlatform.Tests.Sqlite
{
    public sealed class Cl017ExtensionMigrationTests
    {
        private static readonly StorageScope Scope = new StorageScope("test-backend", new PlatformId("01890f3e-7a6b-7c8d-9e0f-102030405060"), new PlatformId("00112233-4455-4677-8899-aabbccddeeff"));

        [Fact]
        public async Task FreshDatabaseInstallsPlatformV4V5AndExplicitExtension()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { Extension(1) }, CancellationToken.None);
            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (Platform: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"), Extensions: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_extension_migrations"), Accounts: Exists(session, "gp_accounts_directory"), Consumer: Exists(session, "consumer_notes"));
            }, CancellationToken.None);
            Assert.Equal(5, observed.Platform);
            Assert.Equal(1, observed.Extensions);
            Assert.Equal(1, observed.Accounts);
            Assert.Equal(1, observed.Consumer);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ExistingExtensionIsRetainedWhilePlatformUpgrades()
        {
            using var files = new TemporaryDatabase();
            var v3 = SqlitePlatformMigrationRegistry.Migrations.Take(3).ToArray();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, v3, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction => { ((SqliteTransactionSession)transaction).Execute("INSERT INTO gp_stream_state VALUES (1,?,?,?,?,1,1,0,0)", Scope.BackendNamespace, Scope.AppId.Value, Scope.AccountId.Value, "0199f9a0-1111-7777-8888-999999999999"); return true; }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { Extension(1) }, CancellationToken.None);
            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (Platform: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"), Ready: session.ExecuteScalar<int>("SELECT ready FROM gp_stream_state"), Extensions: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_extension_migrations"));
            }, CancellationToken.None);
            Assert.Equal(5, observed.Platform);
            Assert.Equal(1, observed.Ready);
            Assert.Equal(1, observed.Extensions);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ExtensionUpgradesIndependentlyOfUnchangedPlatform()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { Extension(1) }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { Extension(2) }, CancellationToken.None);
            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (Markers: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_extension_migrations"), V1: Exists(session, "consumer_notes"), V2: Exists(session, "consumer_tags"));
            }, CancellationToken.None);
            Assert.Equal(2, observed.Markers);
            Assert.Equal(1, observed.V1);
            Assert.Equal(1, observed.V2);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task OmittedNewerDriftAndGapExtensionsFailClosed()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { Extension(2) }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            var omitted = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, omitted.Failure);
            var newer = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { Extension(1) }, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, newer.Failure);
            var drift = new SqliteExtensionDescriptor("consumer.example", 5, new[] { new SqliteExtensionMigration(1, "notes-v1", new[] { "CREATE TABLE consumer_notes (id TEXT PRIMARY KEY, changed INTEGER NOT NULL)" }), new SqliteExtensionMigration(2, "tags-v1", new[] { "CREATE TABLE consumer_tags (id TEXT PRIMARY KEY)" }) });
            var changed = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { drift }, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, changed.Failure);
            Assert.Throws<ArgumentException>(() => new SqliteExtensionDescriptor("consumer.gap", 5, new[] { new SqliteExtensionMigration(2, "gap", new[] { "CREATE TABLE consumer_gap (id TEXT)" }) }));
            Assert.Throws<ArgumentException>(() => new SqliteExtensionMigration(1, "platform-touch", new[] { "CREATE TABLE gp_not_allowed (id TEXT)" }));
        }

        [Fact]
        public async Task ExtensionEffectsAndMarkersAreOneTransaction()
        {
            using var files = new TemporaryDatabase();
            foreach (var checkpoint in new[] { ExtensionMigrationCheckpoint.EffectsApplied, ExtensionMigrationCheckpoint.MarkerInserted })
            {
                var failure = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { Extension(1) }, SqliteDatabaseOptions.Default, null, (at, _, _) => { if (at == checkpoint) throw new InjectedFailure(); }, CancellationToken.None));
                Assert.IsType<InjectedFailure>(failure.InnerException);
                var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
                var observed = await database.ExecuteAsync(Scope, transaction => { var session = (SqliteTransactionSession)transaction; return (Marker: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_extension_migrations"), Table: Exists(session, "consumer_notes")); }, CancellationToken.None);
                Assert.Equal(0, observed.Marker);
                Assert.Equal(0, observed.Table);
                Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
        }

        [Fact]
        public async Task ConcurrentOpenUsesOneExtensionJournalAndChecksumsIncludeDescriptorIdentity()
        {
            using var files = new TemporaryDatabase();
            var extension = Extension(1);
            var first = extension.GetChecksum(extension.Migrations[0]);
            var renamed = new SqliteExtensionDescriptor("consumer.other", 5, extension.Migrations);
            var minimumChanged = new SqliteExtensionDescriptor("consumer.example", 4, extension.Migrations);
            Assert.NotEqual(first, renamed.GetChecksum(renamed.Migrations[0]));
            Assert.NotEqual(first, minimumChanged.GetChecksum(minimumChanged.Migrations[0]));

            var opens = Enumerable.Range(0, 4).Select(_ => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { extension }, CancellationToken.None)).ToArray();
            var databases = await Task.WhenAll(opens);
            var observed = await databases[0].ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_extension_migrations"), CancellationToken.None);
            Assert.Equal(1, observed);
            foreach (var database in databases) Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        private static SqliteExtensionDescriptor Extension(int version) => new SqliteExtensionDescriptor("consumer.example", 5, version == 1
            ? new[] { new SqliteExtensionMigration(1, "notes-v1", new[] { "CREATE TABLE consumer_notes (id TEXT PRIMARY KEY, body TEXT NOT NULL)" }) }
            : new[] { new SqliteExtensionMigration(1, "notes-v1", new[] { "CREATE TABLE consumer_notes (id TEXT PRIMARY KEY, body TEXT NOT NULL)" }), new SqliteExtensionMigration(2, "tags-v1", new[] { "CREATE TABLE consumer_tags (id TEXT PRIMARY KEY, note_id TEXT NOT NULL)" }) });

        private static int Exists(SqliteTransactionSession session, string table) => session.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?", table);
        private sealed class InjectedFailure : Exception { }
        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-extension-migrations", Guid.NewGuid().ToString("N"));
            public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); }
            public string Path { get; }
            public void Dispose()
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                try { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
                catch (IOException) { } // SQLite native handle cleanup is outside migration assertions.
            }
        }
    }
}
