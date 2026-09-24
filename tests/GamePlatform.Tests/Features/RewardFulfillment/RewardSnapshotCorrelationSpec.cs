#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.RewardFulfillment;
using GamePlatform.Features.RewardFulfillment;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using Xunit;

namespace GamePlatform.Tests.Features.RewardFulfillment
{
    public sealed class RewardSnapshotCorrelationSpec
    {
        private static readonly AppId App = new AppId(Guid.Parse("0199f9a0-0700-7000-8000-000000000101"));
        private static readonly PlatformUserId User = new PlatformUserId(Guid.Parse("0199f9a0-0700-7000-8000-000000000102"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("rewards-snapshot-test"), App, User);
        private static readonly StorageScope Scope = new StorageScope("rewards-snapshot-test", new PlatformId(App.ToString()), new PlatformId(User.ToString()));
        private static readonly ScopedOwnerContext Context = new ScopedOwnerContext(Owner, new SemanticId("private"), 9);
        private static readonly ScopedOwnerContext OtherView = new ScopedOwnerContext(Owner, new SemanticId("other"), 9);
        private static readonly OperationId Operation = new OperationId(Guid.Parse("0199f9a0-0700-7000-8000-000000000110"));
        private static readonly Guid Grant = Guid.Parse("0199f9a0-0700-7000-8000-000000000111");
        private const string Source = "gameplay.completion.00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";
        private const long ReceiptRevision = 10;
        private static readonly RewardReceipt Receipt = new RewardReceipt(Grant, Operation, ReceiptRevision, 100, Source, "mrsquare.test.coin", 1,
            new[] { new RewardReceiptLine(0, 1, RewardReceiptLineKind.Currency, new PlatformId("test.coin"), 1, null) });

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SnapshotCoveringTheReceiptRevisionConfirmsOnceAcrossReopen(bool receiptFirst)
        {
            using var files = new TemporaryDatabase();
            await using (var session = await Session.OpenAsync(files.Path, Context))
            {
                if (receiptFirst) await session.ObserveAsync();
                await session.StageSnapshotAsync(12, Coin(12), Other(3));
            }
            await using (var session = await Session.OpenAsync(files.Path, Context))
            {
                if (!receiptFirst) await session.ObserveAsync();
                Assert.Equal(RewardPresentationStatus.Confirmed, await session.StatusAsync());
                var evidence = await session.State.ReadAsync(Context, "reward-fulfillment", "evidence/" + Grant.ToString("N"), CancellationToken.None);
                Assert.Equal(ReceiptRevision, evidence!.Revision);
                Assert.NotNull(await session.Service.TryClaimPresentationAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None));
            }
            await using (var session = await Session.OpenAsync(files.Path, Context))
            {
                await session.StageSnapshotAsync(15, Coin(15));
                await session.ObserveAsync();
                Assert.Equal(RewardPresentationStatus.Confirmed, await session.StatusAsync());
                Assert.Null(await session.Service.TryClaimPresentationAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None));
                Assert.Null(await session.State.ReadAsync(Context, "wallet", "test.coin", CancellationToken.None));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SnapshotBelowTheReceiptRevisionStaysPendingUntilTheFeedGroupArrives(bool receiptFirst)
        {
            using var files = new TemporaryDatabase();
            await using var session = await Session.OpenAsync(files.Path, Context);
            if (receiptFirst) await session.ObserveAsync();
            await session.StageSnapshotAsync(ReceiptRevision - 1, Coin(9));
            if (!receiptFirst) await session.ObserveAsync();
            Assert.Equal(RewardPresentationStatus.AcceptedAwaitingPull, await session.StatusAsync());

            await session.Database.ExecuteAsync(Scope, transaction => { session.Correlation.StageInstalledFeed(transaction, ReceiptRevision, new[] { Coin(10) }); return true; }, CancellationToken.None);
            Assert.Equal(RewardPresentationStatus.Confirmed, await session.StatusAsync());
            await session.StageSnapshotAsync(12, Coin(12));
            Assert.Equal(RewardPresentationStatus.Confirmed, await session.StatusAsync());
        }

        [Fact]
        public async Task SnapshotWithoutTheReceiptResourceStaysPending()
        {
            using var files = new TemporaryDatabase();
            await using var session = await Session.OpenAsync(files.Path, Context);
            await session.ObserveAsync();
            await session.StageSnapshotAsync(12, Other(12));
            Assert.Equal(RewardPresentationStatus.AcceptedAwaitingPull, await session.StatusAsync());
        }

        [Fact]
        public async Task SnapshotOfAnotherViewDoesNotConfirm()
        {
            using var files = new TemporaryDatabase();
            await using (var other = await Session.OpenAsync(files.Path, OtherView))
                await other.StageSnapshotAsync(12, Coin(12));
            await using var session = await Session.OpenAsync(files.Path, Context);
            await session.ObserveAsync();
            Assert.Equal(RewardPresentationStatus.AcceptedAwaitingPull, await session.StatusAsync());
        }

        [Fact]
        public async Task RolledBackSnapshotInstallLeavesTheReceiptPending()
        {
            using var files = new TemporaryDatabase();
            await using var session = await Session.OpenAsync(files.Path, Context);
            await session.ObserveAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.Database.ExecuteAsync<bool>(Scope, transaction =>
            {
                session.Correlation.StageInstalledSnapshot(transaction, 12, new[] { Coin(12) });
                throw new InvalidOperationException("Snapshot install aborted.");
            }, CancellationToken.None));
            Assert.Equal(RewardPresentationStatus.AcceptedAwaitingPull, await session.StatusAsync());
            Assert.Null(await session.State.ReadAsync(Context, "reward-fulfillment", "evidence/" + Grant.ToString("N"), CancellationToken.None));
            Assert.Null(await session.State.ReadAsync(Context, "reward-correlation", "snapshot", CancellationToken.None));
            Assert.Equal(1, (await session.State.ReadAsync(Context, "reward-correlation", "pending", CancellationToken.None))!.Revision);

            await session.ObserveAsync();
            Assert.Equal(RewardPresentationStatus.AcceptedAwaitingPull, await session.StatusAsync());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SnapshotWithManyValueProjectionsConfirmsTheCoveredReceipt(bool receiptFirst)
        {
            using var files = new TemporaryDatabase();
            await using var session = await Session.OpenAsync(files.Path, Context);
            if (receiptFirst) await session.ObserveAsync();
            await session.StageSnapshotAsync(12, Many(199, Coin(12)));
            if (!receiptFirst) await session.ObserveAsync();
            Assert.Equal(RewardPresentationStatus.Confirmed, await session.StatusAsync());
        }

        [Fact]
        public async Task SnapshotAboveTheProjectionBoundIsRejectedAndTheReceiptStaysPending()
        {
            using var files = new TemporaryDatabase();
            await using var session = await Session.OpenAsync(files.Path, Context);
            await session.ObserveAsync();
            await Assert.ThrowsAsync<ArgumentException>(() => session.StageSnapshotAsync(12, Many(1024, Coin(12))));
            Assert.Equal(RewardPresentationStatus.AcceptedAwaitingPull, await session.StatusAsync());
            Assert.Null(await session.State.ReadAsync(Context, "reward-correlation", "snapshot", CancellationToken.None));
        }

        [Fact]
        public async Task LowerSnapshotReplacesTheRetainedSnapshot()
        {
            using var files = new TemporaryDatabase();
            await using var session = await Session.OpenAsync(files.Path, Context);
            await session.StageSnapshotAsync(15, Coin(15));
            await session.StageSnapshotAsync(ReceiptRevision - 1, Coin(9));
            await session.ObserveAsync();
            Assert.Equal(RewardPresentationStatus.AcceptedAwaitingPull, await session.StatusAsync());
            Assert.Equal(ReceiptRevision - 1, (await session.State.ReadAsync(Context, "reward-correlation", "snapshot", CancellationToken.None))!.Revision);
        }

        private static InstalledRewardProjection Coin(long revision) => new InstalledRewardProjection(RewardReceiptLineKind.Currency, new PlatformId("test.coin"), revision);
        private static InstalledRewardProjection[] Many(int others, InstalledRewardProjection covered)
        {
            var result = new InstalledRewardProjection[others + 1];
            for (var i = 0; i < others; i++) result[i] = new InstalledRewardProjection(RewardReceiptLineKind.ItemStack, new PlatformId("test.item." + i), i + 1);
            result[others] = covered;
            return result;
        }

        private static InstalledRewardProjection Other(long revision) => new InstalledRewardProjection(RewardReceiptLineKind.Currency, new PlatformId("test.gem"), revision);

        private sealed class Session : IAsyncDisposable
        {
            private readonly ScopedOwnerContext owner;

            private Session(SqliteDatabase database, ScopedOwnerContext owner)
            {
                this.owner = owner;
                Database = database;
                State = new SqliteDurableFeatureStateStore(database, Scope);
                Service = DurableRewardFulfillmentComposition.Create(owner, Scope, State, database, () => 1_000);
                Correlation = new DurableRewardReceiptCorrelation(owner, Scope, State, database, Service, () => 1_000);
            }

            public SqliteDatabase Database { get; }
            public SqliteDurableFeatureStateStore State { get; }
            public RewardFulfillmentService Service { get; }
            public DurableRewardReceiptCorrelation Correlation { get; }

            public static async Task<Session> OpenAsync(string path, ScopedOwnerContext owner) =>
                new Session(await SqliteDatabase.OpenAsync(path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None), owner);

            public Task ObserveAsync() => Correlation.ObserveReceiptAsync(new RewardReceiptObservation(owner, Operation, Receipt), CancellationToken.None);

            public Task StageSnapshotAsync(long committedThrough, params InstalledRewardProjection[] projections) =>
                Database.ExecuteAsync(Scope, transaction => { Correlation.StageInstalledSnapshot(transaction, committedThrough, projections); return true; }, CancellationToken.None);

            public async Task<RewardPresentationStatus> StatusAsync() =>
                (await Service.ReadAsync(new RewardPresentationQuery(owner, Operation), CancellationToken.None)).Value!.Status;

            public async ValueTask DisposeAsync()
            {
                Service.Dispose();
                Assert.True(await Database.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
        }

        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-reward-snapshot", Guid.NewGuid().ToString("N"));

            public TemporaryDatabase()
            {
                Directory.CreateDirectory(directory);
                Path = System.IO.Path.Combine(directory, "platform.sqlite3");
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
