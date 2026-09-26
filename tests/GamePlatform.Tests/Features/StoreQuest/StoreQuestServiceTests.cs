#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Quests;
using GamePlatform.Features.Store;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;
using GamePlatform.Transport.Http;

namespace GamePlatform.Tests.Features.StoreQuest
{
    public sealed class StoreQuestServiceTests : IAsyncLifetime
    {
        private static readonly AppId App = new AppId(Guid.Parse("0199f9a0-0000-7000-8000-000000000001"));
        private static readonly PlatformUserId User = new PlatformUserId(Guid.Parse("0199f9a0-0000-7000-8000-000000000002"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("store-quest-test"), App, User);
        private static readonly StorageScope Scope = new StorageScope("store-quest-test", new PlatformId(App.ToString()), new PlatformId(User.ToString()));
        private static readonly ScopedOwnerContext Context = new ScopedOwnerContext(Owner, new SemanticId("private"), 1);
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-0000-7000-8000-000000000003"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-0000-7000-8000-000000000004");
        private static readonly Guid PurchaseKey = Guid.Parse("019952d1-0000-7000-8000-0000000000a1");
        private static readonly StoreQuestCapability Capability = new StoreQuestCapability(true);
        private readonly string directory = Path.Combine(Path.GetTempPath(), "game-platform-a06", Guid.NewGuid().ToString("N"));
        private readonly List<SqliteDatabase> databases = new List<SqliteDatabase>();
        private readonly StoreQuestCommandBodyCodec bodies = new StoreQuestCommandBodyCodec(new StoreQuestMessagePackCodec());
        private readonly UuidV7Generator ids = new UuidV7Generator(new FixedClock(), new CryptographicUuidRandomSource());

        public StoreQuestServiceTests() => Directory.CreateDirectory(directory);

        [Fact]
        public async Task RepeatingAPurchaseKeyKeepsOneDurableCommandIdentity()
        {
            var database = await OpenReady();
            var first = await Store(database).PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None);
            var second = await Store(database).PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None);

            Assert.Equal(first.OperationId, second.OperationId);
            Assert.Equal(first.Sequence, second.Sequence);
            Assert.Equal(1, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_outbox"));
            Assert.Equal("store.purchase:" + PurchaseKey.ToString("D"), await Scalar<string>(database, "SELECT business_run_id FROM gp_outbox"));
            Assert.Equal("store.offer.purchase", await Scalar<string>(database, "SELECT operation_kind FROM gp_outbox"));
            var body = await Scalar<byte[]>(database, "SELECT semantic_body FROM gp_outbox");
            var payload = new StoreQuestMessagePackCodec().DecodePurchaseCommand(body);
            Assert.Equal(("test.offer.starter", 1, PurchaseKey), (payload.OfferId, payload.OfferVersion, payload.PurchaseKey));
            Assert.Equal(7, first.OperationId.Value.ToString("D")[14] - '0');
        }

        [Fact]
        public async Task APurchaseKeyRepeatedAfterReopeningTheDatabaseKeepsItsOperation()
        {
            var database = await OpenReady();
            var first = await Store(database).PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None);
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(10)));

            var reopened = await OpenReady(seed: false);
            var second = await Store(reopened).PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None);

            Assert.Equal(first.OperationId, second.OperationId);
            Assert.Equal(first.Sequence, second.Sequence);
            Assert.Equal(1, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_outbox"));
        }

        [Fact]
        public async Task AnotherAccountWithTheSamePurchaseKeyOwnsASeparateCommand()
        {
            var database = await OpenReady();
            var first = await Store(database).PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None);
            var otherOwner = new OwnerScope(Owner.Backend, App, new PlatformUserId(Guid.Parse("0199f9a0-0000-7000-8000-000000000009")));
            var otherScope = new StorageScope("store-quest-test", new PlatformId(App.ToString()), new PlatformId(otherOwner.UserId.ToString()));
            var other = await OpenReady(otherScope, otherOwner, "other.sqlite3");

            var second = await new StoreService(new ScopedOwnerContext(otherOwner, new SemanticId("private"), 1), Stream, new SqliteDurableFeatureStateStore(other, otherScope),
                new SqliteAtomicCommandStore(other, otherScope, otherOwner, Capability.CreateFingerprint(), new FixedClock()), bodies, ids).PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None);

            Assert.NotEqual(first.OperationId, second.OperationId);
            Assert.Equal(1, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_outbox"));
            Assert.Equal(1, await Scalar<long>(other, "SELECT COUNT(*) FROM gp_outbox", otherScope));
        }

        [Fact]
        public async Task AServiceForAnotherAccountCannotWriteThroughThisAccountsOutbox()
        {
            var database = await OpenReady();
            var otherOwner = new OwnerScope(Owner.Backend, App, new PlatformUserId(Guid.Parse("0199f9a0-0000-7000-8000-000000000009")));
            using var store = new StoreService(new ScopedOwnerContext(otherOwner, new SemanticId("private"), 1), Stream, new SqliteDurableFeatureStateStore(database, Scope), Commands(database), bodies, ids);

            var failure = await Assert.ThrowsAnyAsync<Exception>(() => store.PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None));

            Assert.True(failure is StorageException storage && storage.Failure == StorageFailure.InvalidOwner || failure is ArgumentException, failure.GetType().Name);
            Assert.Equal(0, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_outbox"));
        }

        [Fact]
        public async Task AnotherViewCannotReuseThisViewsRetainedOperation()
        {
            var database = await OpenReady();
            await Store(database).PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None);
            using var otherView = new StoreService(new ScopedOwnerContext(Owner, new SemanticId("other-view"), 1), Stream, new SqliteDurableFeatureStateStore(database, Scope), Commands(database), bodies, ids);

            var conflict = await Assert.ThrowsAsync<StorageException>(() => otherView.PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None));

            Assert.Equal(StorageFailure.IdentityConflict, conflict.Failure);
            Assert.Equal(1, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_outbox"));
            Assert.Equal(1, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_feature_state"));
        }

        [Fact]
        public async Task APurchaseKeyReusedForAnotherOfferIsAnIdentityConflict()
        {
            var database = await OpenReady();
            await Store(database).PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None);

            var conflict = await Assert.ThrowsAsync<StorageException>(() => Store(database).PurchaseAsync("test.offer.starter", 2, PurchaseKey, CancellationToken.None));

            Assert.Equal(StorageFailure.IdentityConflict, conflict.Failure);
            Assert.Equal(1, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_outbox"));
        }

        [Fact]
        public async Task ANewPurchaseKeyIsASeparatePurchase()
        {
            var database = await OpenReady();
            var first = await Store(database).PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None);
            var second = await Store(database).PurchaseAsync("test.offer.starter", 1, ids.NewId(), CancellationToken.None);

            Assert.NotEqual(first.OperationId, second.OperationId);
            Assert.Equal(first.Sequence + 1, second.Sequence);
        }

        [Fact]
        public async Task ClaimingAnOccurrenceTwiceKeepsOneDurableCommand()
        {
            var database = await OpenReady();
            var first = await Quests(database).ClaimAsync("test.quest.first-clear", "test.quest.first-clear:occ-1", CancellationToken.None);
            var second = await Quests(database).ClaimAsync("test.quest.first-clear", "test.quest.first-clear:occ-1", CancellationToken.None);

            Assert.Equal(first.OperationId, second.OperationId);
            Assert.Equal(1, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_outbox"));
            Assert.Equal("quest.claim:test.quest.first-clear:occ-1", await Scalar<string>(database, "SELECT business_run_id FROM gp_outbox"));
            Assert.Equal("quest.claim", await Scalar<string>(database, "SELECT operation_kind FROM gp_outbox"));
        }

        [Fact]
        public async Task WithoutTheCapabilityNothingIsAdmitted()
        {
            var database = await OpenReady();
            using var store = new StoreService(Context, Stream, new SqliteDurableFeatureStateStore(database, Scope),
                new SqliteAtomicCommandStore(database, Scope, Owner, new CanonicalCommandFingerprint(), new FixedClock()), bodies, ids);

            await Assert.ThrowsAsync<NotSupportedException>(() => store.PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None));

            Assert.Equal(0, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_outbox"));
            Assert.Equal(0, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_feature_state"));
        }

        [Fact]
        public async Task NonV7PurchaseKeysAreRejected()
        {
            var database = await OpenReady();

            await Assert.ThrowsAsync<ArgumentException>(() => Store(database).PurchaseAsync("test.offer.starter", 1, Guid.Parse("019952d1-0000-4000-8000-0000000000a1"), CancellationToken.None));
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            // Close every native connection, including reopened and alternate-account
            // databases, before deleting SQLite's database/WAL/SHM files.
            for (var index = databases.Count - 1; index >= 0; index--)
            {
                Assert.True(await databases[index].DisposeAsync(TimeSpan.FromSeconds(10)),
                    "The test database did not drain before fixture cleanup.");
            }
            databases.Clear();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }

        private StoreService Store(SqliteDatabase database) => new StoreService(Context, Stream, new SqliteDurableFeatureStateStore(database, Scope), Commands(database), bodies, ids);

        private QuestService Quests(SqliteDatabase database) => new QuestService(Context, Stream, new SqliteDurableFeatureStateStore(database, Scope), Commands(database), bodies, ids);

        private static SqliteAtomicCommandStore Commands(SqliteDatabase database) =>
            new SqliteAtomicCommandStore(database, Scope, Owner, Capability.CreateFingerprint(), new FixedClock());

        private Task<SqliteDatabase> OpenReady(bool seed = true) => OpenReady(Scope, Owner, "platform.sqlite3", seed);

        private async Task<SqliteDatabase> OpenReady(StorageScope scope, OwnerScope owner, string file, bool seed = true)
        {
            var database = await SqliteDatabase.OpenAsync(Path.Combine(directory, file), scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            databases.Add(database);
            if (seed) await database.ExecuteAsync(scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO gp_stream_state(singleton, backend_namespace, app_id, account_id, client_stream_id, installation_id, ready, next_sequence, local_revision, finalized_through) VALUES (1, ?, ?, ?, ?, ?, 1, 1, 0, 0)",
                    owner.Backend.Value, owner.AppId.ToString(), owner.UserId.ToString(), Stream.ToString(), Installation.ToString("D"));
                return true;
            }, CancellationToken.None);
            return database;
        }

        private static Task<T> Scalar<T>(SqliteDatabase database, string sql, StorageScope? scope = null) =>
            database.ExecuteAsync(scope ?? Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<T>(sql), CancellationToken.None);

        private sealed class FixedClock : IUnixMillisecondClock
        {
            public long GetUnixMilliseconds() => 1_789_555_200_000;
        }
    }
}
