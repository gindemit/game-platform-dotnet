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
    public sealed class Cl007P3ExtensionMigrationTests
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
        public async Task RetainedExtensionSurvivesLaterPlatformUpgradeAndOlderPlatformIsRejected()
        {
            using var files = new TemporaryDatabase();
            var extension = Extension(1);
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { extension }, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction => { ((SqliteTransactionSession)transaction).Execute("INSERT INTO consumer_notes(id, body) VALUES ('retained', 'before-v6')"); return true; }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var v6 = SqlitePlatformMigrationRegistry.Migrations.Concat(new[] { new SqliteMigration(6, "platform-review-v6", new[] { "CREATE TABLE platform_review_v6 (id TEXT PRIMARY KEY)" }) }).ToArray();
            database = await SqliteDatabase.OpenAsync(files.Path, Scope, v6, new[] { extension }, CancellationToken.None);
            var observed = await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                return (Platform: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"), Extensions: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_extension_migrations"), Note: session.ExecuteScalar<string>("SELECT body FROM consumer_notes WHERE id = 'retained'"), V6: Exists(session, "platform_review_v6"));
            }, CancellationToken.None);
            Assert.Equal(6, observed.Platform);
            Assert.Equal(1, observed.Extensions);
            Assert.Equal("before-v6", observed.Note);
            Assert.Equal(1, observed.V6);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var newerPlatform = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { extension }, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, newerPlatform.Failure);
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
        public async Task ForeignOwnerMarkerAndOmittedNamespaceAmongMultipleExtensionsFailClosed()
        {
            using var files = new TemporaryDatabase();
            var alpha = OtherExtension("alpha.example", "alpha_notes");
            var consumer = Extension(1);
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { alpha, consumer }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var omitted = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { consumer }, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, omitted.Failure);

            database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { alpha, consumer }, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction => { ((SqliteTransactionSession)transaction).Execute("UPDATE gp_extension_migrations SET account_id = 'foreign-account' WHERE extension_namespace = ?", alpha.ExtensionNamespace); return true; }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            var foreignOwner = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { alpha, consumer }, CancellationToken.None));
            Assert.Equal(StorageFailure.InvalidOwner, foreignOwner.Failure);
        }

        [Fact]
        public async Task DuplicateUnsortedAndUnmetMinimumDescriptorsFailClosed()
        {
            using var files = new TemporaryDatabase();
            var alpha = OtherExtension("alpha.example", "alpha_notes");
            var consumer = Extension(1);
            var unsorted = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { consumer, alpha }, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, unsorted.Failure);
            var duplicate = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { alpha, alpha }, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, duplicate.Failure);
            var unmet = new SqliteExtensionDescriptor("minimum.example", 6, new[] { new SqliteExtensionMigration(1, "minimum-v1", new[] { "CREATE TABLE minimum_notes (id TEXT PRIMARY KEY)" }) });
            var minimumError = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { unmet }, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, minimumError.Failure);
        }

        [Fact]
        public async Task ChangedMinimumPlatformVersionOnRetainedExtensionFailsChecksumIdentity()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { Extension(1) }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            var changedMinimum = new SqliteExtensionDescriptor("consumer.example", 4, Extension(1).Migrations);
            var failure = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, new[] { changedMinimum }, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, failure.Failure);
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

        private static SqliteExtensionDescriptor OtherExtension(string extensionNamespace, string table) => new SqliteExtensionDescriptor(extensionNamespace, 5, new[]
        {
            new SqliteExtensionMigration(1, table + "-v1", new[] { "CREATE TABLE " + table + " (id TEXT PRIMARY KEY)" })
        });

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
