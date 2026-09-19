using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using SQLite;

namespace GamePlatform.Tests.Sqlite
{
    public sealed class Cl007SqliteExecutorTests
    {
        private static readonly StorageScope Scope = new StorageScope(
            "test-backend",
            new PlatformId("01890f3e-7a6b-7c8d-9e0f-102030405060"),
            new PlatformId("00112233-4455-4677-8899-aabbccddeeff"));

        private static readonly IReadOnlyList<SqliteMigration> Migrations = new[]
        {
            new SqliteMigration(1, "foundation-test-state", new[]
            {
                "CREATE TABLE test_parent (id INTEGER PRIMARY KEY, value INTEGER NOT NULL)",
                "CREATE TABLE test_child (id INTEGER PRIMARY KEY, parent_id INTEGER NOT NULL REFERENCES test_parent(id))"
            })
        };

        [Fact]
        public async Task FreshMigrationCommitRollbackForeignKeysAndReopenUseRealSqlite()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(
                files.Path,
                Scope,
                Migrations,
                new SqliteDatabaseOptions(TimeSpan.FromSeconds(2), () => 1_789_555_201_000L),
                CancellationToken.None);
            SqliteTransactionSession? borrowed = null;
            const long exact = 9_007_199_254_740_993L;
            await database.ExecuteAsync(Scope, transaction =>
            {
                borrowed = Assert.IsType<SqliteTransactionSession>(transaction);
                borrowed.Execute("INSERT INTO test_parent(id, value) VALUES (?, ?)", 1, exact);
                return true;
            }, CancellationToken.None);
            Assert.Throws<ObjectDisposedException>(() => borrowed!.ExecuteScalar<int>("SELECT 1"));

            var foreignKey = await Assert.ThrowsAsync<StorageException>(() => database.ExecuteAsync(Scope, transaction =>
            {
                var session = Assert.IsType<SqliteTransactionSession>(transaction);
                session.Execute("INSERT INTO test_child(id, parent_id) VALUES (1, 999)");
                return true;
            }, CancellationToken.None));
            Assert.Equal(StorageFailure.Constraint, foreignKey.Failure);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            var reopened = await SqliteDatabase.OpenAsync(files.Path, Scope, Migrations, CancellationToken.None);
            var state = await reopened.ExecuteAsync(Scope, transaction =>
            {
                var session = Assert.IsType<SqliteTransactionSession>(transaction);
                return (
                    Value: session.ExecuteScalar<long>("SELECT value FROM test_parent WHERE id = 1"),
                    Children: session.ExecuteScalar<int>("SELECT COUNT(*) FROM test_child"),
                    Migrations: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"),
                    AppliedAt: session.ExecuteScalar<long>("SELECT applied_at FROM gp_migrations WHERE version = 1"),
                    ForeignKeys: session.ExecuteScalar<int>("PRAGMA foreign_keys"),
                    Journal: session.ExecuteScalar<string>("PRAGMA journal_mode"),
                    BusyTimeout: session.ExecuteScalar<int>("PRAGMA busy_timeout"));
            }, CancellationToken.None);
            Assert.Equal(exact, state.Value);
            Assert.Equal(0, state.Children);
            Assert.Equal(1, state.Migrations);
            Assert.Equal(1_789_555_201_000L, state.AppliedAt);
            Assert.Equal(1, state.ForeignKeys);
            Assert.Equal("wal", state.Journal, ignoreCase: true);
            Assert.Equal(2_000, state.BusyTimeout);
            Assert.Equal(1, reopened.ConnectionCount);
            Assert.Equal(3_050_001, reopened.NativeVersionNumber);
            Assert.True(await reopened.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task MigrationFaultBeforeCommitRollsBackEffectsAndMarker()
        {
            await VerifyMigrationFault(MigrationCheckpoint.EffectsApplied);
            await VerifyMigrationFault(MigrationCheckpoint.MarkerInserted);
        }

        private static async Task VerifyMigrationFault(MigrationCheckpoint checkpoint)
        {
            using var files = new TemporaryDatabase();
            await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(
                files.Path,
                Scope,
                Migrations,
                SqliteDatabaseOptions.Default,
                (observed, _) => { if (observed == checkpoint) throw new InvalidOperationException("injected migration interruption"); },
                CancellationToken.None));

            var recovered = await SqliteDatabase.OpenAsync(files.Path, Scope, Migrations, CancellationToken.None);
            var counts = await recovered.ExecuteAsync(Scope, transaction =>
            {
                var session = Assert.IsType<SqliteTransactionSession>(transaction);
                return (
                    Markers: session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_migrations"),
                    Tables: session.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'test_parent'"));
            }, CancellationToken.None);
            Assert.Equal(1, counts.Markers);
            Assert.Equal(1, counts.Tables);
            Assert.True(await recovered.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ChangedChecksumAndNewerSchemaAreRejected()
        {
            using var changedFiles = new TemporaryDatabase();
            var initial = await SqliteDatabase.OpenAsync(changedFiles.Path, Scope, Migrations, CancellationToken.None);
            Assert.True(await initial.DisposeAsync(TimeSpan.FromSeconds(5)));
            var changed = new[] { new SqliteMigration(1, "foundation-test-state", new[] { "CREATE TABLE changed (id INTEGER PRIMARY KEY)" }) };
            var checksum = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(changedFiles.Path, Scope, changed, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, checksum.Failure);

            using var newerFiles = new TemporaryDatabase();
            var newerMigrations = new[]
            {
                Migrations[0],
                new SqliteMigration(2, "newer", new[] { "CREATE TABLE newer (id INTEGER PRIMARY KEY)" })
            };
            var newer = await SqliteDatabase.OpenAsync(newerFiles.Path, Scope, newerMigrations, CancellationToken.None);
            Assert.True(await newer.DisposeAsync(TimeSpan.FromSeconds(5)));
            var incompatible = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(newerFiles.Path, Scope, Migrations, CancellationToken.None));
            Assert.Equal(StorageFailure.Migration, incompatible.Failure);
        }

        [Fact]
        public async Task ParallelWritersSerializeOnOneConnection()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, Migrations, CancellationToken.None);
            using var firstEntered = new ManualResetEventSlim(false);
            using var releaseFirst = new ManualResetEventSlim(false);
            var secondEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = database.ExecuteAsync(Scope, transaction =>
            {
                var session = Assert.IsType<SqliteTransactionSession>(transaction);
                session.Execute("INSERT INTO test_parent(id, value) VALUES (1, 1)");
                firstEntered.Set();
                Assert.True(releaseFirst.Wait(TimeSpan.FromSeconds(5)));
                return true;
            }, CancellationToken.None);
            Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)));
            var second = database.ExecuteAsync(Scope, transaction =>
            {
                secondEntered.TrySetResult(true);
                Assert.IsType<SqliteTransactionSession>(transaction).Execute("INSERT INTO test_parent(id, value) VALUES (2, 2)");
                return true;
            }, CancellationToken.None);
            Assert.NotSame(secondEntered.Task, await Task.WhenAny(secondEntered.Task, Task.Delay(100)));
            releaseFirst.Set();
            await Task.WhenAll(first, second);
            Assert.True(await secondEntered.Task);
            Assert.Equal(1, database.MaximumConcurrentTransactions);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task QueuedAndActiveCancellationHaveDistinctAtomicResults()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, Migrations, CancellationToken.None);
            using var firstEntered = new ManualResetEventSlim(false);
            using var releaseFirst = new ManualResetEventSlim(false);
            var first = database.ExecuteAsync(Scope, transaction =>
            {
                firstEntered.Set();
                Assert.True(releaseFirst.Wait(TimeSpan.FromSeconds(5)));
                return true;
            }, CancellationToken.None);
            Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)));
            using var queuedCancellation = new CancellationTokenSource();
            var queued = database.ExecuteAsync(Scope, _ => true, queuedCancellation.Token);
            queuedCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
            releaseFirst.Set();
            await first;

            using var activeEntered = new ManualResetEventSlim(false);
            using var releaseActive = new ManualResetEventSlim(false);
            using var activeCancellation = new CancellationTokenSource();
            var active = database.ExecuteAsync(Scope, transaction =>
            {
                Assert.IsType<SqliteTransactionSession>(transaction).Execute("INSERT INTO test_parent(id, value) VALUES (3, 3)");
                activeEntered.Set();
                Assert.True(releaseActive.Wait(TimeSpan.FromSeconds(5)));
                return true;
            }, activeCancellation.Token);
            Assert.True(activeEntered.Wait(TimeSpan.FromSeconds(5)));
            activeCancellation.Cancel();
            releaseActive.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active);
            var rows = await database.ExecuteAsync(Scope, transaction => Assert.IsType<SqliteTransactionSession>(transaction).ExecuteScalar<int>("SELECT COUNT(*) FROM test_parent WHERE id = 3"), CancellationToken.None);
            Assert.Equal(0, rows);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task DisposalTimeoutClosesAdmissionUntilQuiescent()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, Migrations, CancellationToken.None);
            using var entered = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            var work = database.ExecuteAsync(Scope, transaction =>
            {
                Assert.IsType<SqliteTransactionSession>(transaction).Execute("INSERT INTO test_parent(id, value) VALUES (4, 4)");
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
                return true;
            }, CancellationToken.None);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(await database.DisposeAsync(TimeSpan.Zero));
            var closing = await Assert.ThrowsAsync<StorageException>(() => database.ExecuteAsync(Scope, _ => true, CancellationToken.None));
            Assert.Equal(StorageFailure.Closing, closing.Failure);
            release.Set();
            await work;
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(0, database.ConnectionCount);
        }

        [Fact]
        public async Task OwnerMismatchAndTransactionControlFailClosed()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, Migrations, CancellationToken.None);
            var foreign = new StorageScope("other-backend", Scope.AppId, Scope.AccountId);
            var owner = await Assert.ThrowsAsync<StorageException>(() => database.ExecuteAsync(foreign, _ => true, CancellationToken.None));
            Assert.Equal(StorageFailure.InvalidOwner, owner.Failure);
            await Assert.ThrowsAsync<InvalidOperationException>(() => database.ExecuteAsync(Scope, transaction =>
            {
                Assert.IsType<SqliteTransactionSession>(transaction).Execute("COMMIT");
                return true;
            }, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => database.ExecuteAsync(Scope, transaction =>
            {
                Assert.IsType<SqliteTransactionSession>(transaction).Execute("SELECT 1; COMMIT");
                return true;
            }, CancellationToken.None));
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            var wrongScope = await Assert.ThrowsAsync<StorageException>(() => SqliteDatabase.OpenAsync(files.Path, foreign, Migrations, CancellationToken.None));
            Assert.Equal(StorageFailure.InvalidOwner, wrongScope.Failure);
        }

        [Fact]
        public async Task ExternalWriterLockMapsToTypedBusyFailure()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, Migrations, CancellationToken.None);
            using var external = new SQLiteConnection(files.Path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex);
            external.BusyTimeout = TimeSpan.FromMilliseconds(100);
            external.BeginTransaction();
            external.Execute("INSERT INTO test_parent(id, value) VALUES (10, 10)");
            try
            {
                var busy = await Assert.ThrowsAsync<StorageException>(() => database.ExecuteAsync(Scope, transaction =>
                {
                    Assert.IsType<SqliteTransactionSession>(transaction).Execute("INSERT INTO test_parent(id, value) VALUES (11, 11)");
                    return true;
                }, CancellationToken.None));
                Assert.Equal(StorageFailure.Busy, busy.Failure);
            }
            finally
            {
                external.Rollback();
            }
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl007", Guid.NewGuid().ToString("N"));

            public TemporaryDatabase()
            {
                Directory.CreateDirectory(directory);
                Path = System.IO.Path.Combine(directory, "platform.sqlite3");
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }
    }
}
