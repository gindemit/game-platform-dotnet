using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;

namespace GamePlatform.Tests.Cache
{
    public sealed class Cl011SqliteFeatureStateTests
    {
        private static readonly Guid App = Guid.Parse("0199f9a0-1000-7000-8000-000000000001");
        private static readonly Guid User = Guid.Parse("0199f9a0-1000-7000-8000-000000000002");
        private static readonly StorageScope Scope = new StorageScope("backend", new PlatformId(App.ToString("D")), new PlatformId(User.ToString("D")));

        [Fact]
        public async Task NativeStoreCommitsReopensAndPreservesUnknownExtensions()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            var store = new SqliteDurableFeatureStateStore(database, Scope);
            var owner = Owner("view-a", 1);
            await database.ExecuteAsync(Scope, transaction =>
            {
                store.Upsert(transaction, new DurableFeatureMutation(owner, "profile", "self", 7, 100, new byte[] { 1 }, new byte[] { 8, 9 }));
                return true;
            }, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction =>
            {
                store.Upsert(transaction, new DurableFeatureMutation(owner, "profile", "self", 8, 101, new byte[] { 2 }, null));
                return true;
            }, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            store = new SqliteDurableFeatureStateStore(database, Scope);
            var state = await store.ReadAsync(Owner("view-a", 99), "profile", "self", CancellationToken.None);
            Assert.NotNull(state);
            Assert.Equal(8, state!.Revision);
            Assert.Equal(new byte[] { 2 }, state.CopyPayload());
            Assert.Equal(new byte[] { 8, 9 }, state.CopyExtensions());
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ViewsAreIsolatedAndFailedTransactionDoesNotPublish()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            var store = new SqliteDurableFeatureStateStore(database, Scope);
            await database.ExecuteAsync(Scope, transaction => { store.Upsert(transaction, Mutation(Owner("view-a", 1), 2)); return true; }, CancellationToken.None);
            await Assert.ThrowsAsync<InjectedFailure>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                store.Upsert(transaction, Mutation(Owner("view-b", 1), 3));
                throw new InjectedFailure();
            }, CancellationToken.None));
            Assert.NotNull(await store.ReadAsync(Owner("view-a", 2), "progression", "level:1", CancellationToken.None));
            Assert.Null(await store.ReadAsync(Owner("view-b", 2), "progression", "level:1", CancellationToken.None));
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task RevisionRegressionAndForeignOwnerFailClosed()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            var store = new SqliteDurableFeatureStateStore(database, Scope);
            await database.ExecuteAsync(Scope, transaction => { store.Upsert(transaction, Mutation(Owner("view", 1), 5)); return true; }, CancellationToken.None);
            var regression = await Assert.ThrowsAsync<StorageException>(() => database.ExecuteAsync(Scope, transaction => { store.Upsert(transaction, Mutation(Owner("view", 1), 4)); return true; }, CancellationToken.None));
            Assert.Equal(StorageFailure.IdentityConflict, regression.Failure);
            var foreign = new ScopedOwnerContext(new OwnerScope(new BackendNamespace("backend"), new AppId(App), new PlatformUserId(Guid.Parse("0199f9a0-1000-7000-8000-000000000099"))), new SemanticId("view"), 1);
            var ownerError = await Assert.ThrowsAsync<StorageException>(() => store.ReadAsync(foreign, "progression", "level:1", CancellationToken.None));
            Assert.Equal(StorageFailure.InvalidOwner, ownerError.Failure);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task BorrowedReadAndDeleteRemainInsideCallerTransaction()
        {
            using var files = new TemporaryDatabase();
            var database = await SqliteDatabase.OpenAsync(files.Path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            var store = new SqliteDurableFeatureStateStore(database, Scope); var owner = Owner("view", 1);
            await database.ExecuteAsync(Scope, transaction => { store.Upsert(transaction, Mutation(owner, 5)); return true; }, CancellationToken.None);
            await Assert.ThrowsAsync<InjectedFailure>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                Assert.Equal(5, store.Read(transaction, owner, "progression", "level:1")!.Revision);
                store.Delete(transaction, owner, "progression", "level:1");
                Assert.Null(store.Read(transaction, owner, "progression", "level:1"));
                throw new InjectedFailure();
            }, CancellationToken.None));
            Assert.Equal(5, (await store.ReadAsync(owner, "progression", "level:1", CancellationToken.None))!.Revision);
            await database.ExecuteAsync(Scope, transaction => { store.Delete(transaction, owner, "progression", "level:1"); return true; }, CancellationToken.None);
            Assert.Null(await store.ReadAsync(owner, "progression", "level:1", CancellationToken.None));
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void PayloadsAreDefensivelyCopiedAndBounded()
        {
            var payload = new byte[] { 1 }; var extensions = new byte[] { 2 };
            var mutation = new DurableFeatureMutation(Owner("view", 1), "profile", "self", 1, 1, payload, extensions);
            payload[0] = 9; extensions[0] = 9;
            Assert.Equal(new byte[] { 1 }, mutation.CopyPayload());
            Assert.Equal(new byte[] { 2 }, mutation.CopyExtensions());
            Assert.Throws<ArgumentOutOfRangeException>(() => new DurableFeatureMutation(Owner("view", 1), "profile", "self", 1, 1, Array.Empty<byte>(), new byte[65_537]));
        }

        private static DurableFeatureMutation Mutation(ScopedOwnerContext owner, long revision) => new DurableFeatureMutation(owner, "progression", "level:1", revision, 100, new byte[] { (byte)revision }, Array.Empty<byte>());
        private static ScopedOwnerContext Owner(string view, long generation) => new ScopedOwnerContext(new OwnerScope(new BackendNamespace("backend"), new AppId(App), new PlatformUserId(User)), new SemanticId(view), generation);
        private sealed class InjectedFailure : Exception { }
        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl011", Guid.NewGuid().ToString("N"));
            public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); }
            public string Path { get; }
            public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
