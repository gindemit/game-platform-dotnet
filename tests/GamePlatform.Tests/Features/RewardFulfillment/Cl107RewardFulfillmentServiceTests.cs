#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.RewardFulfillment;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.RewardFulfillment;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;

namespace GamePlatform.Tests.Features.RewardFulfillment
{
    /// <summary>These tests use the pinned native SQLite feature-state store. Receipt/group inputs are explicit trusted-boundary test fixtures, not local grant mocks.</summary>
    public sealed class Cl107RewardFulfillmentServiceTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("0199f9a0-0700-7000-8000-000000000001"));
        private static readonly PlatformUserId User = new PlatformUserId(Guid.Parse("0199f9a0-0700-7000-8000-000000000002"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("rewards-test"), App, User);
        private static readonly StorageScope Scope = new StorageScope("rewards-test", new PlatformId(App.ToString()), new PlatformId(User.ToString()));
        private static readonly ScopedOwnerContext Context = new ScopedOwnerContext(Owner, new SemanticId("private"), 9);
        private static readonly OperationId Operation = new OperationId(Guid.Parse("0199f9a0-0700-7000-8000-000000000010"));
        private static readonly Guid Grant = Guid.Parse("0199f9a0-0700-7000-8000-000000000011");
        private const string Source = "gameplay.completion.0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Fact]
        public async Task ReceiptCommitReopenThenLaterFeedConfirmsWithoutReceiptReplayOrReconcileAndNeverAppliesQuantity()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var state = new SqliteDurableFeatureStateStore(database, Scope); var service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000); var correlation = new DurableRewardReceiptCorrelation(Context, Scope, state, database, service, () => 1_000); var receipt = Receipt(Operation, Grant, Source);
            await correlation.ObserveReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            database = await OpenAsync(files.Path); state = new SqliteDurableFeatureStateStore(database, Scope); service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000); correlation = new DurableRewardReceiptCorrelation(Context, Scope, state, database, service, () => 1_000);
            await database.ExecuteAsync(Scope, transaction => { correlation.StageInstalledFeed(transaction, receipt.FeedRevision, Projections()); return true; }, CancellationToken.None);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            database = await OpenAsync(files.Path); state = new SqliteDurableFeatureStateStore(database, Scope); service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000);
            var result = await service.ReadAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None); Assert.Equal(RewardPresentationStatus.Confirmed, result.Value!.Status); Assert.Null(await state.ReadAsync(Context, "wallet", "test.coin", CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task FeedCommitReopenThenLaterReceiptConfirmsWithoutFeedReplayAndRejectsChangedReplay()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var state = new SqliteDurableFeatureStateStore(database, Scope); var service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000); var correlation = new DurableRewardReceiptCorrelation(Context, Scope, state, database, service, () => 1_000); var receipt = Receipt(Operation, Grant, Source);
            await database.ExecuteAsync(Scope, transaction => { correlation.StageInstalledFeed(transaction, receipt.FeedRevision, Projections()); return true; }, CancellationToken.None);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            database = await OpenAsync(files.Path); state = new SqliteDurableFeatureStateStore(database, Scope); service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000); correlation = new DurableRewardReceiptCorrelation(Context, Scope, state, database, service, () => 1_000);
            await correlation.ObserveReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None); var result = await service.ReadAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None); Assert.Equal(RewardPresentationStatus.Confirmed, result.Value!.Status);
            await Assert.ThrowsAsync<RewardFulfillmentConflictException>(() => database.ExecuteAsync(Scope, transaction => { correlation.StageInstalledFeed(transaction, receipt.FeedRevision, new[] { new InstalledRewardProjection(RewardReceiptLineKind.Currency, new PlatformId("test.coin"), 999) }); return true; }, CancellationToken.None));
            await database.ExecuteAsync(Scope, transaction => { correlation.StageInstalledFeed(transaction, receipt.FeedRevision, Projections()); return true; }, CancellationToken.None);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5))); database = await OpenAsync(files.Path); state = new SqliteDurableFeatureStateStore(database, Scope); service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000); Assert.Equal(RewardPresentationStatus.Confirmed, (await service.ReadAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None)).Value!.Status); service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task RevisionZeroProjectionCorrelatesAcrossRestartWhileFeedRevisionMustRemainPositive()
        {
            var projection = new InstalledRewardProjection(RewardReceiptLineKind.Currency,
                new PlatformId("test.coin"), 0);
            var groupLine = new RewardProjectionLine(0, RewardReceiptLineKind.Currency,
                new PlatformId("test.coin"), 0);
            Assert.Equal(0, projection.ProjectionRevision);
            Assert.Equal(0, groupLine.ProjectionRevision);
            Assert.Throws<ArgumentOutOfRangeException>(() => new InstalledRewardProjection(
                RewardReceiptLineKind.Currency, new PlatformId("test.coin"), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardProjectionLine(0,
                RewardReceiptLineKind.Currency, new PlatformId("test.coin"), -1));

            using var files = new TemporaryDatabase();
            var receipt = CurrencyReceipt(Operation, Grant, Source, 10);
            var database = await OpenAsync(files.Path);
            var state = new SqliteDurableFeatureStateStore(database, Scope);
            var service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000);
            var correlation = new DurableRewardReceiptCorrelation(Context, Scope, state, database, service, () => 1_000);
            await correlation.ObserveReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            service.Dispose();
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            database = await OpenAsync(files.Path);
            state = new SqliteDurableFeatureStateStore(database, Scope);
            service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000);
            correlation = new DurableRewardReceiptCorrelation(Context, Scope, state, database, service, () => 1_000);
            await database.ExecuteAsync(Scope, transaction =>
            {
                correlation.StageInstalledFeed(transaction, receipt.FeedRevision, new[] { projection });
                return true;
            }, CancellationToken.None);
            Assert.Equal(RewardPresentationStatus.Confirmed,
                (await service.ReadAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None)).Value!.Status);
            Assert.Null(await state.ReadAsync(Context, "wallet", "test.coin", CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => database.ExecuteAsync(Scope, transaction =>
            {
                correlation.StageInstalledFeed(transaction, 0, Array.Empty<InstalledRewardProjection>());
                return true;
            }, CancellationToken.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => CurrencyReceipt(Operation, Grant, Source, 0));
            service.Dispose();
            Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task EmptyFeedMarkerReopensReplaysAndRejectsReceiptsInBothOrders()
        {
            using (var files = new TemporaryDatabase())
            {
                var database = await OpenAsync(files.Path);
                var state = new SqliteDurableFeatureStateStore(database, Scope);
                var service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000);
                var correlation = new DurableRewardReceiptCorrelation(Context, Scope, state, database, service, () => 1_000);
                const long feedRevision = 20;
                await database.ExecuteAsync(Scope, transaction =>
                {
                    correlation.StageInstalledFeed(transaction, feedRevision, Array.Empty<InstalledRewardProjection>());
                    return true;
                }, CancellationToken.None);
                service.Dispose();
                Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

                database = await OpenAsync(files.Path);
                state = new SqliteDurableFeatureStateStore(database, Scope);
                service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000);
                correlation = new DurableRewardReceiptCorrelation(Context, Scope, state, database, service, () => 1_000);
                var marker = await state.ReadAsync(Context, "reward-correlation", "feed/" + feedRevision, CancellationToken.None);
                Assert.NotNull(marker);
                await database.ExecuteAsync(Scope, transaction =>
                {
                    correlation.StageInstalledFeed(transaction, feedRevision, Array.Empty<InstalledRewardProjection>());
                    return true;
                }, CancellationToken.None);
                await Assert.ThrowsAsync<RewardFulfillmentConflictException>(() => database.ExecuteAsync(Scope, transaction =>
                {
                    correlation.StageInstalledFeed(transaction, feedRevision, new[]
                    {
                        new InstalledRewardProjection(RewardReceiptLineKind.Currency, new PlatformId("test.coin"), 1)
                    });
                    return true;
                }, CancellationToken.None));
                Assert.Equal(marker!.Revision, (await state.ReadAsync(Context, "reward-correlation", "feed/" + feedRevision, CancellationToken.None))!.Revision);
                var receipt = CurrencyReceipt(Operation, Grant, Source, feedRevision);
                await Assert.ThrowsAsync<RewardFulfillmentConflictException>(() => correlation.ObserveReceiptAsync(
                    new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None));
                Assert.Equal(FeatureSnapshotState.Missing,
                    (await service.ReadAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None)).State);
                Assert.Null(await state.ReadAsync(Context, "reward-correlation", "receipt/" + feedRevision, CancellationToken.None));
                Assert.NotNull(await state.ReadAsync(Context, "reward-correlation", "feed/" + feedRevision, CancellationToken.None));
                service.Dispose();
                Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            }

            using (var files = new TemporaryDatabase())
            {
                var database = await OpenAsync(files.Path);
                var state = new SqliteDurableFeatureStateStore(database, Scope);
                var service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000);
                var correlation = new DurableRewardReceiptCorrelation(Context, Scope, state, database, service, () => 1_000);
                const long feedRevision = 21;
                var receipt = CurrencyReceipt(Operation, Grant, Source, feedRevision);
                await correlation.ObserveReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
                service.Dispose();
                Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

                database = await OpenAsync(files.Path);
                state = new SqliteDurableFeatureStateStore(database, Scope);
                service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000);
                correlation = new DurableRewardReceiptCorrelation(Context, Scope, state, database, service, () => 1_000);
                await Assert.ThrowsAsync<RewardFulfillmentConflictException>(() => database.ExecuteAsync(Scope, transaction =>
                {
                    correlation.StageInstalledFeed(transaction, feedRevision, Array.Empty<InstalledRewardProjection>());
                    return true;
                }, CancellationToken.None));
                Assert.Null(await state.ReadAsync(Context, "reward-correlation", "feed/" + feedRevision, CancellationToken.None));
                Assert.NotNull(await state.ReadAsync(Context, "reward-correlation", "receipt/" + feedRevision, CancellationToken.None));
                Assert.Equal(RewardPresentationStatus.AcceptedAwaitingPull,
                    (await service.ReadAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None)).Value!.Status);
                service.Dispose();
                Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
        }

        [Fact]
        public async Task InjectedReceiptCorrelationFaultRollsBackReceiptAndPresentationThenRetryCommitsBoth()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var durable = new SqliteDurableFeatureStateStore(database, Scope); var faulting = new FaultOnceState(durable); var service = DurableRewardFulfillmentComposition.Create(Context, Scope, faulting, database, () => 1_000); var correlation = new DurableRewardReceiptCorrelation(Context, Scope, faulting, database, service, () => 1_000); var receipt = Receipt(Operation, Grant, Source);
            await Assert.ThrowsAsync<InvalidOperationException>(() => correlation.ObserveReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None));
            Assert.Null(await durable.ReadAsync(Context, "reward-fulfillment", "operation/" + Operation, CancellationToken.None)); Assert.Null(await durable.ReadAsync(Context, "reward-correlation", "receipt/" + receipt.FeedRevision, CancellationToken.None));
            await correlation.ObserveReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            Assert.NotNull(await durable.ReadAsync(Context, "reward-fulfillment", "operation/" + Operation, CancellationToken.None)); Assert.NotNull(await durable.ReadAsync(Context, "reward-correlation", "receipt/" + receipt.FeedRevision, CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task FaultBeforeFeedCommitRollsBackFeedEvidenceConfirmationAndCursorSentinel()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var state = new SqliteDurableFeatureStateStore(database, Scope); var service = DurableRewardFulfillmentComposition.Create(Context, Scope, state, database, () => 1_000); var correlation = new DurableRewardReceiptCorrelation(Context, Scope, state, database, service, () => 1_000); var receipt = Receipt(Operation, Grant, Source);
            await correlation.ObserveReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => database.ExecuteAsync<bool>(Scope, transaction => { var session = (SqliteTransactionSession)transaction; session.Execute("INSERT INTO gp_sync_state VALUES (1,?,1,?,10,NULL,0,NULL)", "0199f9a0-0700-7000-8000-000000000020", new byte[] { 2 }); correlation.StageInstalledFeed(transaction, receipt.FeedRevision, Projections()); throw new InvalidOperationException("fault before feed commit"); }, CancellationToken.None));
            Assert.Null(await state.ReadAsync(Context, "reward-correlation", "feed/" + receipt.FeedRevision, CancellationToken.None)); Assert.Null(await state.ReadAsync(Context, "reward-fulfillment", "evidence/" + Grant.ToString("N"), CancellationToken.None)); Assert.Equal(0, await database.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_sync_state"), CancellationToken.None));
            Assert.Equal(RewardPresentationStatus.AcceptedAwaitingPull, (await service.ReadAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None)).Value!.Status);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task RealSqliteAcceptedBeforePullConfirmsOnlyMatchingGroupAndPresentsExactlyOnceAfterRestart()
        {
            using var files = new TemporaryDatabase();
            var database = await OpenAsync(files.Path);
            var service = Create(database, Context);
            var receipt = Receipt(Operation, Grant, Source);
            await service.BeginPendingAsync(new RewardPresentationQuery(Context, Operation), Source, CancellationToken.None);
            var accepted = await service.ObserveAcceptedReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Pending, accepted.State);
            Assert.Equal(RewardPresentationStatus.AcceptedAwaitingPull, accepted.Value!.Status);

            var confirmed = await service.ObserveProjectionGroupAsync(Group(Context, Operation, receipt), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Stale, confirmed.State);
            Assert.Equal(RewardPresentationStatus.Confirmed, confirmed.Value!.Status);
            Assert.NotNull(await service.TryClaimPresentationAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None));
            Assert.Null(await service.TryClaimPresentationAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            database = await OpenAsync(files.Path); service = Create(database, Context);
            var restored = await service.ReadAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None);
            Assert.True(restored.Value!.Presented);
            Assert.Null(await service.TryClaimPresentationAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task SameBusinessSourceDifferentOperationUsesOneGrantAndNeverDoublePresents()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database, Context);
            var other = new OperationId(Guid.Parse("0199f9a0-0700-7000-8000-000000000012")); var receipt = Receipt(Operation, Grant, Source);
            await service.ObserveAcceptedReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            await service.ObserveProjectionGroupAsync(Group(Context, Operation, receipt), CancellationToken.None);
            await service.ObserveAcceptedReceiptAsync(new RewardReceiptObservation(Context, other, receipt), CancellationToken.None);
            Assert.NotNull(await service.TryClaimPresentationAsync(new RewardPresentationQuery(Context, other), CancellationToken.None));
            Assert.Null(await service.TryClaimPresentationAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ResponseLossGroupBeforeReceiptReconcilesAndRepeatedReceiptIsIdempotent()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database, Context); var receipt = Receipt(Operation, Grant, Source);
            var before = await service.ObserveProjectionGroupAsync(Group(Context, Operation, receipt), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Missing, before.State);
            var first = await service.ObserveAcceptedReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            var duplicate = await service.ObserveAcceptedReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            Assert.Equal(RewardPresentationStatus.Confirmed, first.Value!.Status); Assert.Equal(RewardPresentationStatus.Confirmed, duplicate.Value!.Status);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task PartialOrMalformedGroupAndTerminalRejectionFailClosedWithoutValueMutation()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database, Context); var receipt = Receipt(Operation, Grant, Source);
            await service.ObserveAcceptedReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            var partial = new RewardProjectionGroup(Context, Operation, Grant, Source, receipt.FeedRevision, new[] { new RewardProjectionLine(0, RewardReceiptLineKind.Currency, new PlatformId("test.coin"), 7) });
            await Assert.ThrowsAsync<RewardFulfillmentConflictException>(() => service.ObserveProjectionGroupAsync(partial, CancellationToken.None));
            Assert.Equal(RewardPresentationStatus.AcceptedAwaitingPull, (await service.ReadAsync(new RewardPresentationQuery(Context, Operation), CancellationToken.None)).Value!.Status);

            var rejectedOperation = new OperationId(Guid.Parse("0199f9a0-0700-7000-8000-000000000013"));
            var rejected = await service.ObserveRejectedAsync(new RewardRejection(Context, rejectedOperation, Source, "reward_denied"), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Error, rejected.State); Assert.Equal(RewardPresentationStatus.Rejected, rejected.Value!.Status);
            var rows = await database.ExecuteAsync(Scope, tx => ((GamePlatform.Storage.Sqlite.Executor.SqliteTransactionSession)tx).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE feature_namespace='reward-fulfillment'"), CancellationToken.None);
            Assert.Equal(2, rows); // one accepted receipt and one rejected state; malformed group evidence is not retained, and no value projection is touched.
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task ForeignOwnerAndConflictingReceiptAreRejected()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database, Context); var receipt = Receipt(Operation, Grant, Source);
            var otherOwner = new ScopedOwnerContext(new OwnerScope(Owner.Backend, App, new PlatformUserId(Guid.Parse("0199f9a0-0700-7000-8000-000000000099"))), new SemanticId("private"), 1);
            await Assert.ThrowsAsync<RewardFulfillmentOwnerMismatchException>(() => service.BeginPendingAsync(new RewardPresentationQuery(otherOwner, Operation), Source, CancellationToken.None));
            await service.ObserveAcceptedReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            var altered = Receipt(Operation, Guid.Parse("0199f9a0-0700-7000-8000-000000000099"), Source);
            await Assert.ThrowsAsync<RewardFulfillmentConflictException>(() => service.ObserveAcceptedReceiptAsync(new RewardReceiptObservation(Context, Operation, altered), CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task BorrowedProjectionTransactionRollsBackOrCommitsWithCursorSentinelThenReconcilesLaterReceipt()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database, Context); var receipt = Receipt(Operation, Grant, Source); var group = Group(Context, Operation, receipt);
            await Assert.ThrowsAsync<InvalidOperationException>(() => database.ExecuteAsync<bool>(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                session.Execute("INSERT INTO gp_sync_state VALUES (1,?,1,?,0,NULL,0,NULL)", "0199f9a0-0700-7000-8000-000000000020", new byte[] { 1 });
                service.StageProjectionGroup(transaction, group);
                throw new InvalidOperationException("simulate cursor transaction rollback");
            }, CancellationToken.None));
            Assert.Equal(0, await database.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE feature_namespace='reward-fulfillment'"), CancellationToken.None));
            Assert.Equal(0, await database.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<int>("SELECT COUNT(*) FROM gp_sync_state"), CancellationToken.None));

            await database.ExecuteAsync(Scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                session.Execute("INSERT INTO gp_sync_state VALUES (1,?,1,?,10,NULL,0,NULL)", "0199f9a0-0700-7000-8000-000000000020", new byte[] { 2 });
                service.StageProjectionGroup(transaction, group);
                return true;
            }, CancellationToken.None);
            Assert.Equal(10, await database.ExecuteAsync(Scope, tx => ((SqliteTransactionSession)tx).ExecuteScalar<long>("SELECT committed_through FROM gp_sync_state WHERE singleton=1"), CancellationToken.None));
            var laterReceipt = await service.ObserveAcceptedReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            Assert.Equal(RewardPresentationStatus.Confirmed, laterReceipt.Value!.Status);
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task TwoServiceInstancesRaceForOneSqliteClaimAndReopenCannotClaimAgain()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var one = Create(database, Context); var two = Create(database, Context); var receipt = Receipt(Operation, Grant, Source);
            await one.ObserveAcceptedReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            await one.ObserveProjectionGroupAsync(Group(Context, Operation, receipt), CancellationToken.None);
            var query = new RewardPresentationQuery(Context, Operation);
            var claims = await Task.WhenAll(one.TryClaimPresentationAsync(query, CancellationToken.None), two.TryClaimPresentationAsync(query, CancellationToken.None));
            Assert.Single(claims, value => value != null);
            one.Dispose(); two.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            database = await OpenAsync(files.Path); var reopened = Create(database, Context);
            Assert.Null(await reopened.TryClaimPresentationAsync(query, CancellationToken.None));
            reopened.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task BorrowedEvidenceInsertOrVerifyRejectsEqualRevisionReplacementAndPreservesOriginalAcrossReopen()
        {
            using var files = new TemporaryDatabase(); var database = await OpenAsync(files.Path); var service = Create(database, Context); var receipt = Receipt(Operation, Grant, Source); var original = Group(Context, Operation, receipt);
            await database.ExecuteAsync(Scope, transaction => { service.StageProjectionGroup(transaction, original); return true; }, CancellationToken.None);
            await database.ExecuteAsync(Scope, transaction => { service.StageProjectionGroup(transaction, original); return true; }, CancellationToken.None); // exact replay
            var altered = new RewardProjectionGroup(Context, new OperationId(Guid.Parse("0199f9a0-0700-7000-8000-000000000014")), Grant, "gameplay.completion.changed", receipt.FeedRevision,
                new[] { new RewardProjectionLine(0, RewardReceiptLineKind.Currency, new PlatformId("test.coin"), 999), new RewardProjectionLine(1, RewardReceiptLineKind.ItemStack, new PlatformId("test.item"), 11) });
            await Assert.ThrowsAsync<RewardFulfillmentConflictException>(() => database.ExecuteAsync(Scope, transaction => { service.StageProjectionGroup(transaction, altered); return true; }, CancellationToken.None));
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));

            database = await OpenAsync(files.Path); service = Create(database, Context);
            var restored = await service.ObserveAcceptedReceiptAsync(new RewardReceiptObservation(Context, Operation, receipt), CancellationToken.None);
            Assert.Equal(RewardPresentationStatus.Confirmed, restored.Value!.Status); // original evidence, not altered bytes, survived reopen.
            service.Dispose(); Assert.True(await database.DisposeAsync(TimeSpan.FromSeconds(5)));
        }

        private static RewardFulfillmentService Create(SqliteDatabase database, ScopedOwnerContext owner) => DurableRewardFulfillmentComposition.Create(owner, Scope, new SqliteDurableFeatureStateStore(database, Scope), database, () => 1_000);
        private static Task<SqliteDatabase> OpenAsync(string path) => SqliteDatabase.OpenAsync(path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
        private static RewardReceipt Receipt(OperationId originating, Guid grant, string source) => new RewardReceipt(grant, originating, 10, 100, source, "mrsquare.test.coin", 1, new RewardReceiptLine[]
        {
            new RewardReceiptLine(0, 1, RewardReceiptLineKind.Currency, new PlatformId("test.coin"), 1, null),
            new RewardReceiptLine(1, 1, RewardReceiptLineKind.ItemStack, new PlatformId("test.item"), 2, null)
        });
        private static RewardReceipt CurrencyReceipt(OperationId originating, Guid grant, string source, long feedRevision) =>
            new RewardReceipt(grant, originating, feedRevision, 100, source, "mrsquare.test.coin", 1,
                new[] { new RewardReceiptLine(0, 1, RewardReceiptLineKind.Currency, new PlatformId("test.coin"), 2, null) });
        private static RewardProjectionGroup Group(ScopedOwnerContext owner, OperationId operation, RewardReceipt receipt) => new RewardProjectionGroup(owner, operation, receipt.GrantId, receipt.BusinessSource, receipt.FeedRevision, receipt.Lines.Select(line => new RewardProjectionLine(line.LineIndex, line.Kind, line.ResourceId, 10 + line.LineIndex)));
        private static InstalledRewardProjection[] Projections() => new[] { new InstalledRewardProjection(RewardReceiptLineKind.Currency, new PlatformId("test.coin"), 10), new InstalledRewardProjection(RewardReceiptLineKind.ItemStack, new PlatformId("test.item"), 11) };

        private sealed class Codec : IRewardFulfillmentStateCodec
        {
            public byte[] EncodeRecord(RewardPresentationRecord value)
            {
                var receipt = value.Receipt == null ? Array.Empty<string>() : new[] { value.Receipt.GrantId.ToString("N"), value.Receipt.OriginatingOperationId.ToString(), value.Receipt.FeedRevision.ToString(), value.Receipt.RecordedAtMilliseconds.ToString(), value.Receipt.PlanId, value.Receipt.PlanVersion.ToString(), string.Join("~", value.Receipt.Lines.Select(Line)) };
                return Encode(new[] { value.OperationId.ToString(), value.BusinessSource, ((int)value.Status).ToString(), value.RejectionCode ?? "", value.Presented ? "1" : "0" }.Concat(receipt));
            }
            public RewardPresentationRecord DecodeRecord(long _, ReadOnlySpan<byte> payload)
            {
                var values = Decode(payload); if (values.Length != 5 && values.Length != 12) throw new InvalidOperationException("Invalid reward test state.");
                var status = (RewardPresentationStatus)int.Parse(values[2]); RewardReceipt? receipt = null;
                if (values.Length == 12)
                {
                    var lines = values[11].Split(new[] { '~' }, StringSplitOptions.RemoveEmptyEntries).Select(ParseLine).ToArray();
                    receipt = new RewardReceipt(Guid.ParseExact(values[5], "N"), new OperationId(Guid.Parse(values[6])), long.Parse(values[7]), long.Parse(values[8]), values[1], values[9], int.Parse(values[10]), lines);
                }
                return new RewardPresentationRecord(new OperationId(Guid.Parse(values[0])), values[1], status, receipt, string.IsNullOrEmpty(values[3]) ? null : values[3], values[4] == "1");
            }
            public byte[] EncodeGroup(RewardProjectionGroup group) => Encode(new[] { group.OperationId.ToString(), group.GrantId.ToString("N"), group.BusinessSource, group.FeedRevision.ToString(), string.Join("~", group.Lines.Select(line => string.Join(",", line.LineIndex, (int)line.Kind, line.ResourceId.Value, line.ProjectionRevision))) });
            public RewardProjectionGroup DecodeGroup(ScopedOwnerContext owner, long _, ReadOnlySpan<byte> payload)
            {
                var values = Decode(payload); if (values.Length != 5) throw new InvalidOperationException("Invalid reward group test state.");
                return new RewardProjectionGroup(owner, new OperationId(Guid.Parse(values[0])), Guid.ParseExact(values[1], "N"), values[2], long.Parse(values[3]), values[4].Split('~').Select(value => { var p = value.Split(','); return new RewardProjectionLine(int.Parse(p[0]), (RewardReceiptLineKind)int.Parse(p[1]), new PlatformId(p[2]), long.Parse(p[3])); }));
            }
            private static string Line(RewardReceiptLine value) => string.Join(",", value.LineIndex, value.DefinitionVersion, (int)value.Kind, value.ResourceId.Value, value.Quantity?.ToString() ?? "", value.ExpiresAtServerMilliseconds?.ToString() ?? "");
            private static RewardReceiptLine ParseLine(string value) { var p = value.Split(','); return new RewardReceiptLine(int.Parse(p[0]), int.Parse(p[1]), (RewardReceiptLineKind)int.Parse(p[2]), new PlatformId(p[3]), string.IsNullOrEmpty(p[4]) ? null : long.Parse(p[4]), string.IsNullOrEmpty(p[5]) ? null : long.Parse(p[5])); }
            private static byte[] Encode(IEnumerable<string> values) => Encoding.UTF8.GetBytes(string.Join("|", values.Select(value => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)))));
            private static string[] Decode(ReadOnlySpan<byte> value) => Encoding.UTF8.GetString(value).Split('|').Select(item => Encoding.UTF8.GetString(Convert.FromBase64String(item))).ToArray();
        }
        /// <summary>Concrete test composition for the feature-owned conditional-claim port. It deliberately uses INSERT OR IGNORE, not feature-state Upsert.</summary>
        private sealed class SqliteClaimStore : IRewardPresentationClaimStore
        {
            private readonly SqliteDatabase database;
            public SqliteClaimStore(SqliteDatabase database) { this.database = database ?? throw new ArgumentNullException(nameof(database)); }
            public Task<bool> TryClaimAsync(ScopedOwnerContext owner, Guid grantId, Action<ILocalStorageTransaction> onClaim, CancellationToken cancellationToken)
            {
                if (!owner.IsValid || grantId == Guid.Empty || onClaim == null) throw new ArgumentException("Invalid presentation claim.");
                return database.ExecuteAsync(Scope, transaction =>
                {
                    var session = (SqliteTransactionSession)transaction;
                    var inserted = session.Execute("INSERT OR IGNORE INTO gp_feature_state(view_key,feature_namespace,entity_key,revision,confirmed_at,payload,extensions) VALUES (?,?,?,?,?,?,?)",
                        owner.ViewKey.Value, "reward-fulfillment", "presentation/" + grantId.ToString("N"), 1L, 1_000L, new byte[] { 1 }, Array.Empty<byte>());
                    if (inserted != 1) return false;
                    onClaim(transaction);
                    return true;
                }, cancellationToken);
            }
        }
        /// <summary>Concrete test composition for immutable group evidence. Equal feed revision is accepted only when canonical bytes match exactly.</summary>
        private sealed class SqliteEvidenceStore : IRewardProjectionEvidenceStore
        {
            public void InsertOrVerify(ILocalStorageTransaction transaction, ScopedOwnerContext owner, Guid grantId, long feedRevision, long recordedAtMilliseconds, byte[] canonicalPayload)
            {
                var session = transaction as SqliteTransactionSession ?? throw new InvalidOperationException("Expected the SQLite transaction.");
                if (canonicalPayload == null || canonicalPayload.Length == 0) throw new ArgumentException("A canonical group payload is required.", nameof(canonicalPayload));
                var key = "evidence/" + grantId.ToString("N");
                var args = new object[] { owner.ViewKey.Value, "reward-fulfillment", key };
                if (session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args) == 0)
                {
                    session.Execute("INSERT INTO gp_feature_state(view_key,feature_namespace,entity_key,revision,confirmed_at,payload,extensions) VALUES (?,?,?,?,?,?,?)",
                        owner.ViewKey.Value, "reward-fulfillment", key, feedRevision, recordedAtMilliseconds, canonicalPayload, Array.Empty<byte>());
                    return;
                }
                var existingRevision = session.ExecuteScalar<long>("SELECT revision FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args);
                var existingPayload = session.ExecuteScalar<byte[]>("SELECT payload FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args);
                if (existingRevision != feedRevision || !SameBytes(existingPayload, canonicalPayload)) throw new RewardFulfillmentConflictException("A grant cannot replace immutable projection evidence.");
            }
            private static bool SameBytes(byte[] left, byte[] right)
            {
                if (left.Length != right.Length) return false;
                var difference = 0; for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
                return difference == 0;
            }
        }
        private sealed class FaultOnceState : IDurableFeatureStateStore
        {
            private readonly IDurableFeatureStateStore inner; private bool pending = true;
            public FaultOnceState(IDurableFeatureStateStore inner) { this.inner = inner ?? throw new ArgumentNullException(nameof(inner)); }
            public Task<DurableFeatureState?> ReadAsync(ScopedOwnerContext owner, string featureNamespace, string entityKey, CancellationToken token) => inner.ReadAsync(owner, featureNamespace, entityKey, token);
            public DurableFeatureState? Read(ILocalStorageTransaction transaction, ScopedOwnerContext owner, string featureNamespace, string entityKey) => inner.Read(transaction, owner, featureNamespace, entityKey);
            public void Upsert(ILocalStorageTransaction transaction, DurableFeatureMutation mutation)
            {
                inner.Upsert(transaction, mutation);
                if (pending && mutation.FeatureNamespace == "reward-correlation" && mutation.EntityKey.StartsWith("receipt/", StringComparison.Ordinal)) { pending = false; throw new InvalidOperationException("injected receipt correlation fault"); }
            }
            public void Delete(ILocalStorageTransaction transaction, ScopedOwnerContext owner, string featureNamespace, string entityKey) => inner.Delete(transaction, owner, featureNamespace, entityKey);
        }
        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-cl107", Guid.NewGuid().ToString("N"));
            public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); }
            public string Path { get; }
            public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
