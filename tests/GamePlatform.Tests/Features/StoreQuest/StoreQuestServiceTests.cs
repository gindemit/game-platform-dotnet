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
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;
using GamePlatform.Storage.Sqlite.Sync;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;

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
        public async Task NativeCheckpointRequiresReadyPrivateViewAndNoStagedBootstrap()
        {
            var database = await OpenReady();
            var reader = new SqliteNativeInstalledCheckpointReader(database, Scope);
            Assert.Null(await reader.ReadAsync(Context, CancellationToken.None));
            var epoch = Guid.Parse("0199f9a0-0000-7000-8000-000000000006");
            await database.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO gp_sync_state VALUES (1,?,1,?,42,NULL,3,?)", Stream.ToString(), new byte[] { 1 }, epoch.ToString("D"));
                return true;
            }, CancellationToken.None);
            var checkpoint = await reader.ReadAsync(Context, CancellationToken.None);
            Assert.NotNull(checkpoint);
            Assert.Equal((42L, 3L, epoch), (checkpoint!.CommittedThrough, checkpoint.VisibilityGeneration, checkpoint.LogEpoch));
            await Assert.ThrowsAsync<StorageException>(() => reader.ReadAsync(new ScopedOwnerContext(Owner, new SemanticId("other"), 1), CancellationToken.None));
            await database.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO gp_bootstrap_state VALUES (1,?,'active',0,1,?,?,42,3,?,1,'profile')", Stream.ToString(), new byte[] { 1 }, new byte[] { 2 }, epoch.ToString("D"));
                return true;
            }, CancellationToken.None);
            Assert.Null(await reader.ReadAsync(Context, CancellationToken.None));
        }

        [Fact]
        public async Task UnboundLegacyPurchaseIsVisibleButNeverClaimsDelivery()
        {
            var database = await OpenReady();
            var admission = await Store(database).PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None);
            var result = new StoreOfferPurchasedResult(ids.NewId(), admission.OperationId.Value, "test.offer.starter", 1,
                PurchaseKey, new StoreOfferDebit("test.coin", 10), new ILiveSliceRewardLine[] { new LiveSliceCurrencyRewardLine(0, 1, "test.coin", 1) });
            var terminal = new StoreQuestMessagePackCodec().EncodeResult(new PushAccepted(admission.OperationId.Value, admission.Sequence, 42, result));
            await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                session.Execute("INSERT INTO gp_sync_state VALUES (1,?,1,?,42,NULL,1,?)", Stream.ToString(), new byte[] { 1 }, ids.NewId().ToString("D"));
                session.Execute("UPDATE gp_outbox SET delivery_state='accepted', terminal_result=? WHERE operation_id=?", terminal, admission.OperationId.ToString());
                return true;
            }, CancellationToken.None);
            using var observer = new StorePurchaseObserver(Context, new SqliteStorePurchaseRecordReader(database, Scope,
                new SqliteDurableFeatureStateStore(database, Scope)), new StorePurchaseObservationCodec());
            Assert.Equal("AcceptedAwaitingProjection", Assert.Single(await observer.ReadAsync(Context, CancellationToken.None)).Status);
        }

        [Fact]
        public async Task BoundPurchaseSurvivesRestartAndOnlyConfirmsCompatibleInstalledCheckpoint()
        {
            var database = await OpenReady();
            var epoch = Guid.Parse("0199f9a0-0000-7000-8000-000000000006");
            await database.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("INSERT INTO gp_sync_state VALUES (1,?,1,?,0,NULL,1,?)", Stream.ToString(), new byte[] { 1 }, epoch.ToString("D"));
                return true;
            }, CancellationToken.None);
            var featureState = new SqliteDurableFeatureStateStore(database, Scope);
            using var service = new StoreService(Context, Stream, featureState, Commands(database), bodies, ids, new SqliteStorePurchaseAdmissionBinding(featureState));
            var admission = await service.PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None);
            var result = new StoreOfferPurchasedResult(ids.NewId(), admission.OperationId.Value, "test.offer.starter", 1,
                PurchaseKey, new StoreOfferDebit("test.coin", 10), new ILiveSliceRewardLine[] {
                    new LiveSliceCurrencyRewardLine(0, 1, "test.coin", 5),
                    new LiveSliceCurrencyRewardLine(1, 1, "test.coin", 5) });
            var terminal = new StoreQuestMessagePackCodec().EncodeResult(new PushAccepted(admission.OperationId.Value, admission.Sequence, 42, result));
            await database.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("UPDATE gp_outbox SET delivery_state='accepted', terminal_result=? WHERE operation_id=?", terminal, admission.OperationId.ToString());
                return true;
            }, CancellationToken.None);
            await database.DisposeAsync(TimeSpan.FromSeconds(10));
            var reopened = await OpenReady(seed: false);
            var freshOwner = new ScopedOwnerContext(Owner, new SemanticId("private"), 2);
            using var observer = new StorePurchaseObserver(freshOwner, new SqliteStorePurchaseRecordReader(reopened, Scope,
                new SqliteDurableFeatureStateStore(reopened, Scope)), new StorePurchaseObservationCodec());
            Assert.Equal("AcceptedAwaitingProjection", Assert.Single(await observer.ReadAsync(freshOwner, CancellationToken.None)).Status);
            await reopened.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("UPDATE gp_sync_state SET committed_through=42");
                return true;
            }, CancellationToken.None);
            Assert.Equal("Delivered", Assert.Single(await observer.ReadAsync(freshOwner, CancellationToken.None)).Status);
            await reopened.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("UPDATE gp_sync_state SET visibility_generation=2");
                return true;
            }, CancellationToken.None);
            Assert.Equal("AcceptedAwaitingProjection", Assert.Single(await observer.ReadAsync(freshOwner, CancellationToken.None)).Status);
            await reopened.ExecuteAsync(Scope, transaction =>
            {
                ((SqliteTransactionSession)transaction).Execute("UPDATE gp_sync_state SET visibility_generation=1");
                ((SqliteTransactionSession)transaction).Execute("UPDATE gp_sync_state SET log_epoch=?", ids.NewId().ToString("D"));
                return true;
            }, CancellationToken.None);
            Assert.Equal("AcceptedAwaitingProjection", Assert.Single(await observer.ReadAsync(freshOwner, CancellationToken.None)).Status);
        }

        [Fact]
        public async Task BindingFailureRollsBackAdmissionAndSequence()
        {
            var database = await OpenReady();
            using var store = new StoreService(Context, Stream, new SqliteDurableFeatureStateStore(database, Scope),
                Commands(database), bodies, ids, new FailingBinding());

            await Assert.ThrowsAsync<InvalidOperationException>(() => store.PurchaseAsync("test.offer.starter", 1, PurchaseKey, CancellationToken.None));

            Assert.Equal(0, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_outbox"));
            Assert.Equal(0, await Scalar<long>(database, "SELECT COUNT(*) FROM gp_feature_state"));
            Assert.Equal(1, await Scalar<long>(database, "SELECT next_sequence FROM gp_stream_state"));
        }

        private sealed class FailingBinding : IStorePurchaseAdmissionBinding
        {
            public void Stage(ILocalStorageTransaction transaction, ScopedOwnerContext owner, OperationId operationId, long localRevision) =>
                throw new InvalidOperationException("binding failure");
        }

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
            Assert.Equal(1, await Scalar<long>(reopened, "SELECT COUNT(*) FROM gp_outbox"));
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
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
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
            if (seed)
                await database.ExecuteAsync(scope, transaction =>
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
