#nullable enable
using System;
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
    public sealed class StoreQuestServiceTests : IDisposable
    {
        private static readonly AppId App = new AppId(Guid.Parse("0199f9a0-0000-7000-8000-000000000001"));
        private static readonly PlatformUserId User = new PlatformUserId(Guid.Parse("0199f9a0-0000-7000-8000-000000000002"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("store-quest-test"), App, User);
        private static readonly StorageScope Scope = new StorageScope("store-quest-test", new PlatformId(App.ToString()), new PlatformId(User.ToString()));
        private static readonly ScopedOwnerContext Context = new ScopedOwnerContext(Owner, new SemanticId("private"), 1);
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-0000-7000-8000-000000000003"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-0000-7000-8000-000000000004");
        private static readonly Guid PurchaseKey = Guid.Parse("019952d1-0000-7000-8000-0000000000a1");
        private readonly string directory = Path.Combine(Path.GetTempPath(), "game-platform-a06", Guid.NewGuid().ToString("N"));
        private readonly StoreQuestCommandBodyCodec bodies = new StoreQuestCommandBodyCodec(new StoreQuestMessagePackCodec());
        private readonly UuidV7Generator ids = new UuidV7Generator(new FixedClock(), new CryptographicUuidRandomSource());

        public StoreQuestServiceTests() => Directory.CreateDirectory(directory);

        [Fact]
        public async Task RepeatingAPurchaseKeyKeepsOneDurableCommandIdentityAcrossReopen()
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

        public void Dispose()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }

        private StoreService Store(SqliteDatabase database) => new StoreService(Context, Stream, new SqliteDurableFeatureStateStore(database, Scope), Commands(database), bodies, ids);

        private QuestService Quests(SqliteDatabase database) => new QuestService(Context, Stream, new SqliteDurableFeatureStateStore(database, Scope), Commands(database), bodies, ids);

        private static SqliteAtomicCommandStore Commands(SqliteDatabase database) =>
            new SqliteAtomicCommandStore(database, Scope, Owner, new CanonicalCommandFingerprint(true), new FixedClock());

        private async Task<SqliteDatabase> OpenReady()
        {
            var database = await SqliteDatabase.OpenAsync(Path.Combine(directory, "platform.sqlite3"), Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO gp_stream_state(singleton, backend_namespace, app_id, account_id, client_stream_id, installation_id, ready, next_sequence, local_revision, finalized_through) VALUES (1, ?, ?, ?, ?, ?, 1, 1, 0, 0)",
                    Owner.Backend.Value, Owner.AppId.ToString(), Owner.UserId.ToString(), Stream.ToString(), Installation.ToString("D"));
                return true;
            }, CancellationToken.None);
            return database;
        }

        private static Task<T> Scalar<T>(SqliteDatabase database, string sql) =>
            database.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<T>(sql), CancellationToken.None);

        private sealed class FixedClock : IUnixMillisecondClock
        {
            public long GetUnixMilliseconds() => 1_789_555_200_000;
        }
    }
}
