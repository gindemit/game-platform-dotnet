using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Purchases;
using GamePlatform.Storage.Abstractions.Sync;

namespace GamePlatform.Tests.Features.Purchases
{
    public sealed class NativePurchaseCoordinatorTests
    {
        private static readonly ScopedOwnerContext Owner = new ScopedOwnerContext(
            new OwnerScope(new BackendNamespace("native-test"), new AppId(Guid.Parse("0199f9a0-0000-7000-8000-000000000001")),
                new PlatformUserId(Guid.Parse("0199f9a0-0000-7000-8000-000000000002"))), new SemanticId("private"), 1);
        private static readonly NativeProduct Product = new NativeProduct("product.one", 1, "test.package", "test.sku", "test");
        private static readonly NativeStoreProduct Price = new NativeStoreProduct("test.package", "test.sku", "$1.00");
        [Fact]
        public async Task PendingNativeTokenIsProtectedAndSubmittedWithoutClaimingGrant()
        {
            var steps = new List<string>();
            var store = new TestStore(steps);
            var bridge = new TestBridge(steps) { PendingWithProof = true };
            using var coordinator = new NativePurchaseCoordinator(Owner, new TestRemote(steps), bridge, store, new Checkpoint(),
                new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource()));
            var result = await coordinator.StartAsync(Owner, Product, Price, CancellationToken.None);
            Assert.Equal(NativeLaunchStatus.Unresolved, result.Status);
            var retained = Assert.Single(store.Records);
            Assert.Null(retained.ReservedAttemptKey);
            Assert.Null(retained.FinalizedChargeId);
            Assert.NotEqual(Guid.Empty, retained.RecoveryIntentKey);
            Assert.Equal(Product.PackageName, retained.PackageName);
            Assert.Equal(Product.StoreProductId, retained.StoreProductId);
            var attempt = Assert.Single(await store.ReadAttemptsAsync(Owner, CancellationToken.None));
            Assert.Equal(result.IntentKey, attempt.IntentKey);
            Assert.True(attempt.LaunchRequested);
            Assert.Equal("awaiting_proof", attempt.Intent!.State);
            Assert.Null(attempt.Intent.GrantId);
            Assert.Contains("submit", steps);
        }
        [Fact]
        public async Task SameSkuCallbackDoesNotProveItBelongsToCurrentIntent()
        {
            var steps = new List<string>();
            var store = new TestStore(steps);
            var remote = new TestRemote(steps) { GrantOnSubmit = true };
            var bridge = new TestBridge(steps);
            using var coordinator = new NativePurchaseCoordinator(Owner, remote, bridge, store, new Checkpoint(),
                new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource()));
            var first = await coordinator.StartAsync(Owner, Product, Price, CancellationToken.None);
            Assert.Equal(NativeLaunchStatus.Unresolved, first.Status);
            Assert.Null(Assert.Single(store.Records).ReservedAttemptKey);
            bridge.ReturnPending = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync(Owner, Product, Price, CancellationToken.None));
            var second = await coordinator.StartAsync(Owner, Product, Price, CancellationToken.None, true);
            Assert.NotEqual(first.IntentKey, second.IntentKey);
        }
        [Fact]
        public async Task GrantedPriorAttemptAllowsNextPurchaseWithoutAcknowledgement()
        {
            var steps = new List<string>();
            var store = new TestStore(steps);
            var oldKey = Guid.Parse("0199f9a0-0000-7000-8000-000000000010");
            var prior = new NativeIntentState(Guid.Parse("0199f9a0-0000-7000-8000-000000000003"), oldKey, Product.ProductId,
                Product.Version, Product.PackageName, Product.StoreProductId, Product.Environment,
                Guid.Parse("0199f9a0-0000-7000-8000-000000000004"), "purchase", false, "granted", null, "pending",
                1, 1, Guid.Parse("0199f9a0-0000-7000-8000-000000000005"), Guid.Parse("0199f9a0-0000-7000-8000-000000000006"),
                Guid.Parse("0199f9a0-0000-7000-8000-000000000007"));
            store.Seed(new NativeAttempt(oldKey, Product, prior, true));
            using var coordinator = new NativePurchaseCoordinator(Owner, new TestRemote(steps), new TestBridge(steps) { ReturnPending = true },
                store, new Checkpoint(), new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource()));
            Assert.NotEqual(oldKey, (await coordinator.StartAsync(Owner, Product, Price, CancellationToken.None)).IntentKey);
        }
        [Fact]
        public async Task ConcurrentAcknowledgedStartsCannotQueueSecondSheet()
        {
            var steps = new List<string>();
            var store = new TestStore(steps);
            var bridge = new TestBridge(steps) { ReturnPending = true, HoldLaunch = true };
            using var coordinator = new NativePurchaseCoordinator(Owner, new TestRemote(steps), bridge, store, new Checkpoint(),
                new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource()));
            var first = coordinator.StartAsync(Owner, Product, Price, CancellationToken.None, true);
            await bridge.LaunchStarted.Task;
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync(Owner, Product, Price, CancellationToken.None, true));
            bridge.LaunchRelease.TrySetResult(true);
            await first;
            Assert.Single(steps.FindAll(step => step == "launch"));
        }
        [Fact]
        public async Task OrphanRecoveryDoesNotClosePriorAttemptButExplicitNewPurchaseCanProceed()
        {
            var steps = new List<string>();
            var store = new TestStore(steps);
            var oldKey = Guid.Parse("0199f9a0-0000-7000-8000-000000000010");
            store.Seed(new NativeAttempt(oldKey, Product, null, true));
            var bridge = new TestBridge(steps) { ReturnUnfinished = true, ReturnPending = true };
            using var coordinator = new NativePurchaseCoordinator(Owner, new TestRemote(steps), bridge, store, new Checkpoint(),
                new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource()));
            await coordinator.RecoverAsync(Owner, CancellationToken.None);
            Assert.Equal(oldKey, (await store.ReadAttemptsAsync(Owner, CancellationToken.None))[0].IntentKey);
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync(Owner, Product, Price, CancellationToken.None));
            var next = await coordinator.StartAsync(Owner, Product, Price, CancellationToken.None, true);
            Assert.NotEqual(oldKey, next.IntentKey);
            Assert.Equal(NativeLaunchStatus.Pending, next.Status);
        }
        [Fact]
        public async Task PartialProviderScanStillProcessesValidProofsAndReportsUnresolved()
        {
            var steps = new List<string>();
            var store = new TestStore(steps);
            var bridge = new TestBridge(steps) { ReturnUnfinished = true, ScanUnresolved = 1 };
            using var coordinator = new NativePurchaseCoordinator(Owner, new TestRemote(steps), bridge, store, new Checkpoint(),
                new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource()));
            var error = await Assert.ThrowsAsync<NativeRecoveryException>(() => coordinator.RecoverAsync(Owner, CancellationToken.None));
            Assert.Equal(1, error.UnresolvedCount);
            Assert.Single(store.Records);
            Assert.Contains("submit", steps);
        }
        [Fact]
        public async Task FailedRecoveryRecordDoesNotPreventOtherProofs()
        {
            var steps = new List<string>();
            var store = new TestStore(steps);
            store.Records.Add(new ProtectedNativeProofRecord(Guid.NewGuid(), Guid.Parse("0199f9a0-0000-7000-8000-000000000010"), "test.package", "test.sku"));
            store.Records.Add(new ProtectedNativeProofRecord(Guid.NewGuid(), Guid.Parse("0199f9a0-0000-7000-8000-000000000011"), "test.package", "test.sku"));
            var remote = new TestRemote(steps) { FailFirstRecovery = true };
            using var coordinator = new NativePurchaseCoordinator(Owner, remote, new TestBridge(steps), store, new Checkpoint(),
                new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource()));
            var error = await Assert.ThrowsAsync<NativeRecoveryException>(() => coordinator.RecoverAsync(Owner, CancellationToken.None));
            Assert.Equal(1, error.UnresolvedCount);
            Assert.Collection(steps.FindAll(step => step == "submit"), _ => { }, _ => { });
        }
        [Fact]
        public async Task ExplicitContinueCanLaunchPreparedAttemptButNeverRelaunchIt()
        {
            var steps = new List<string>();
            var store = new TestStore(steps);
            using var coordinator = Create(steps, store);
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync(Owner, Product,
                new NativeStoreProduct("test.package", "test.sku", "$0.99"), CancellationToken.None));
            var attempt = Assert.Single(await store.ReadAttemptsAsync(Owner, CancellationToken.None));
            await coordinator.ContinuePreparedAsync(Owner, attempt.IntentKey, new NativeStoreProduct("test.package", "test.sku", "$1.00"), CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ContinuePreparedAsync(Owner, attempt.IntentKey,
                new NativeStoreProduct("test.package", "test.sku", "$1.00"), CancellationToken.None));
            Assert.Single(steps.FindAll(step => step == "launch"));
        }
        [Fact]
        public async Task ChangedPriceNeverOpensNativeSheet()
        {
            var steps = new List<string>();
            using var coordinator = Create(steps, new TestStore(steps));
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync(Owner, Product,
                new NativeStoreProduct("test.package", "test.sku", "$0.99"), CancellationToken.None));
            Assert.DoesNotContain("launch", steps);
            Assert.DoesNotContain("launch-marker", steps);
        }
        [Fact]
        public async Task RecoveryReusesTokenIdentityAcrossRepeatedProviderScans()
        {
            var steps = new List<string>();
            var store = new TestStore(steps);
            var bridge = new TestBridge(steps) { ReturnUnfinished = true };
            using (var first = new NativePurchaseCoordinator(Owner, new TestRemote(steps), bridge, store, new Checkpoint(),
                new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource())))
                await first.RecoverAsync(Owner, CancellationToken.None);
            var originalKey = Assert.Single(store.Records).RecoveryIntentKey;
            using (var second = new NativePurchaseCoordinator(Owner, new TestRemote(steps), bridge, store, new Checkpoint(),
                new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource())))
                await second.RecoverAsync(Owner, CancellationToken.None);
            Assert.Equal(originalKey, Assert.Single(store.Records).RecoveryIntentKey);
            Assert.Equal(2, steps.FindAll(step => step == "submit").Count);
        }
        [Fact]
        public async Task DeliveryRequiresExactInstalledVisibilityAndEpoch()
        {
            var steps = new List<string>();
            var remote = new TestRemote(steps);
            var epoch = Guid.Parse("0199f9a0-0000-7000-8000-000000000006");
            var intentId = Guid.Parse("0199f9a0-0000-7000-8000-000000000003");
            remote.Status = new NativeIntentState(intentId, Guid.Parse("0199f9a0-0000-7000-8000-000000000007"), Product.ProductId, 1,
                Product.PackageName, Product.StoreProductId, Product.Environment, Guid.Parse("0199f9a0-0000-7000-8000-000000000004"),
                "recovery", false, "granted", null, "pending", 42, 2, epoch,
                Guid.Parse("0199f9a0-0000-7000-8000-000000000008"), Guid.Parse("0199f9a0-0000-7000-8000-000000000009"));
            var checkpoint = new Checkpoint { Value = new NativeInstalledCheckpoint(41, 2, epoch) };
            using var coordinator = new NativePurchaseCoordinator(Owner, remote, new TestBridge(steps), new TestStore(steps),
                checkpoint, new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource()));
            Assert.False(await coordinator.IsDeliveredAsync(Owner, intentId, CancellationToken.None));
            checkpoint.Value = new NativeInstalledCheckpoint(42, 3, epoch);
            Assert.False(await coordinator.IsDeliveredAsync(Owner, intentId, CancellationToken.None));
            checkpoint.Value = new NativeInstalledCheckpoint(42, 2, epoch);
            Assert.True(await coordinator.IsDeliveredAsync(Owner, intentId, CancellationToken.None));
        }
        [Fact]
        public async Task LaunchOccursOnlyAfterDurableReservationIntentAndLaunchMarker()
        {
            var steps = new List<string>();
            var store = new TestStore(steps);
            using var coordinator = Create(steps, store);
            var result = await coordinator.StartAsync(Owner, Product, new NativeStoreProduct("test.package", "test.sku", "$1.00"), CancellationToken.None);
            Assert.Equal(NativeLaunchStatus.Unresolved, result.Status);
            Assert.Equal(new[] { "reserve", "intent", "commit", "launch-marker", "launch", "protect", "submit" }, steps);
            Assert.Single(store.Records);
        }
        [Fact]
        public async Task FailedReservationNeverOpensStore()
        {
            var steps = new List<string>();
            var store = new TestStore(steps) { FailReserve = true };
            using var coordinator = Create(steps, store);
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync(Owner, Product, new NativeStoreProduct("test.package", "test.sku", "$1.00"), CancellationToken.None));
            Assert.Equal(new[] { "reserve" }, steps);
        }
        [Fact]
        public async Task AllocatedKeyIsOnlyLocalCorrelationAndExactLookupDistinguishesReservationFailure()
        {
            var steps = new List<string>();
            var store = new TestStore(steps) { FailReserve = true };
            using var coordinator = Create(steps, store);
            Guid? allocated = null;
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync(Owner, Product,
                new NativeStoreProduct("test.package", "test.sku", "$1.00"), CancellationToken.None,
                onAttemptAllocated: value => { Assert.Null(allocated); allocated = value; Assert.Empty(steps); }));
            Assert.NotNull(allocated);
            Assert.Null(await coordinator.FindAttemptAsync(Owner, allocated!.Value, CancellationToken.None));
            Assert.DoesNotContain("launch", steps);
        }
        [Fact]
        public async Task AllocatedKeyFindsDurableAttemptWhenFreshPriceValidationFails()
        {
            var steps = new List<string>();
            var store = new TestStore(steps);
            using var coordinator = Create(steps, store);
            Guid? allocated = null;
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync(Owner, Product,
                new NativeStoreProduct("test.package", "test.sku", "$99.00"), CancellationToken.None,
                onAttemptAllocated: value => allocated = value));
            var attempt = await coordinator.FindAttemptAsync(Owner, allocated!.Value, CancellationToken.None);
            Assert.NotNull(attempt);
            Assert.NotNull(attempt!.Intent);
            Assert.False(attempt.LaunchRequested);
            Assert.DoesNotContain("launch", steps);
        }
        [Fact]
        public async Task FinalizedLateCallbackNeverOpensCompactedProofOrSubmitsAgain()
        {
            var steps = new List<string>();
            var store = new TestStore(steps) { ThrowOnOpen = true };
            store.Records.Add(new ProtectedNativeProofRecord(Guid.Parse("0199f9a0-0000-7000-8000-000000000008"), Guid.Parse("0199f9a0-0000-7000-8000-000000000007"), Product.PackageName,
                Product.StoreProductId, finalizedChargeId: Guid.Parse("0199f9a0-0000-7000-8000-000000000009")));
            using var coordinator = Create(steps, store);
            await coordinator.StartAsync(Owner, Product, new NativeStoreProduct("test.package", "test.sku", "$1.00"), CancellationToken.None);
            Assert.DoesNotContain("submit", steps);
            Assert.Null((await coordinator.FindAttemptAsync(Owner, Assert.Single(await store.ReadAttemptsAsync(Owner, CancellationToken.None)).IntentKey, CancellationToken.None))!.Intent!.ChargeId);
            Assert.Throws<ArgumentException>(() => new ProtectedNativeProofRecord(Guid.NewGuid(), Guid.NewGuid(), "pkg", "sku", finalizedChargeId: Guid.Empty));
            Assert.Throws<ArgumentException>(() => new ProtectedNativeProofRecord(Guid.NewGuid(), Guid.Parse("0199f9a0-0000-7000-8000-000000000007"), Product.PackageName, Product.StoreProductId,
                finalizedChargeId: Guid.Parse("0199f9a0-0000-7000-8000-000000000009")));
        }
        [Fact]
        public async Task ProtectedPagesVisitCapturedHighWaterIncrementallyAndRejectDrift()
        {
            var steps = new List<string>();
            var store = new PagedTestStore(steps);
            for (int i = 0; i < 65; i++)
                store.PagedAttempts.Add(new NativeAttempt(Guid.NewGuid(), Product, null, false));
            using var coordinator = new NativePurchaseCoordinator(Owner, new TestRemote(steps), new TestBridge(steps), store,
                new Checkpoint(), new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource()));
            int count = 0;
            await coordinator.VisitAttemptsAsync(Owner, (item, ct) => { if (count++ == 0) store.PagedAttempts.Add(new NativeAttempt(Guid.NewGuid(), Product, null, false)); return Task.CompletedTask; }, CancellationToken.None);
            Assert.Equal(65, count);
            Assert.Equal(3, store.PageCalls);
            store.Drift = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.VisitAttemptsAsync(Owner, (item, ct) => Task.CompletedTask, CancellationToken.None));
            _ = new NativeProtectedPage<NativeAttempt>(Array.Empty<NativeAttempt>(), "next", 64, 0, 32);
            _ = new NativeProtectedPage<NativeAttempt>(Array.Empty<NativeAttempt>(), null, 64, 32, 64);
            Assert.Throws<ArgumentException>(() => new NativeProtectedPage<NativeAttempt>(Array.Empty<NativeAttempt>(), null, 65, 0, 0));
            Assert.Throws<ArgumentException>(() => new NativeProtectedPage<NativeAttempt>(Array.Empty<NativeAttempt>(), "next", 64, 0, 0));
        }
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task CancelledLaunchRetainsProofForFreshCoordinatorRecovery(bool continuePrepared, bool cancelBeforeProof)
        {
            var steps = new List<string>();
            var store = new StatefulProofStore { HoldProtection = true };
            var bridge = new TestBridge(steps) { HoldLaunch = true };
            using var caller = new CancellationTokenSource();
            using (var first = StatefulCoordinator(new TestRemote(steps), bridge, store))
            {
                Guid? preparedKey = null;
                if (continuePrepared)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => first.StartAsync(Owner, Product,
                        new NativeStoreProduct(Product.PackageName, Product.StoreProductId, "$0.99"), CancellationToken.None));
                    preparedKey = Assert.Single(store.Attempts.Values).IntentKey;
                }
                var operation = continuePrepared
                    ? first.ContinuePreparedAsync(Owner, preparedKey!.Value, Price, caller.Token)
                    : first.StartAsync(Owner, Product, Price, caller.Token);
                try
                {
                    await bridge.LaunchStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    if (cancelBeforeProof)
                        caller.Cancel();
                    bridge.LaunchRelease.SetResult(true);
                    await store.ProtectionCommitted.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    Assert.Equal(CancellationToken.None, store.ProtectionToken);
                    var retained = Assert.Single(store.Records);
                    Assert.Null(retained.ReservedAttemptKey);
                    Assert.Equal("synthetic", store.Proofs[retained.ProofId].Token);
                    Assert.False(operation.IsCompleted);
                    caller.Cancel();
                    store.ProtectionRelease.SetResult(true);
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
                    Assert.DoesNotContain("submit", steps);
                    Assert.Null(Assert.Single(store.Records).FinalizedChargeId);
                    Assert.True(Assert.Single(store.Attempts.Values).LaunchRequested);
                }
                finally
                {
                    caller.Cancel();
                    bridge.LaunchRelease.TrySetResult(true);
                    store.ProtectionRelease.TrySetResult(true);
                }
            }
            var original = Assert.Single(store.Records);
            var recovery = new StatefulRecoveryRemote(store);
            using (var fresh = StatefulCoordinator(recovery, bridge, store))
            {
                await fresh.RecoverAsync(Owner, CancellationToken.None);
                await fresh.RecoverAsync(Owner, CancellationToken.None);
            }
            var submission = Assert.Single(recovery.Submissions);
            Assert.Equal(original.RecoveryIntentKey, submission.Key);
            Assert.Equal(original.ProofId, submission.ProofId);
            Assert.Equal(StatefulCharge(original.ProofId), Assert.Single(store.Records).FinalizedChargeId);
            Assert.Equal(original.ProofId, Assert.Single(store.Finalizations));
            Assert.Single(steps, step => step == "launch");
        }
        [Fact]
        public async Task ProofRecoveryPagesKeepSnapshotAndFreshCoordinatorProcessesLaterAppend()
        {
            var steps = new List<string>();
            var store = new StatefulProofStore();
            for (int i = 1; i <= 65; i++)
                store.SeedProof(i);
            var original = store.Records.ToArray();
            var remote = new StatefulRecoveryRemote(store) { OnSubmission = count => { if (count == 1) store.SeedProof(66); } };
            var bridge = new TestBridge(steps);
            using (var first = StatefulCoordinator(remote, bridge, store))
                await first.RecoverAsync(Owner, CancellationToken.None);
            Assert.Equal(new[] { (0L, 32L, 65L), (32L, 64L, 65L), (64L, 65L, 65L) }, store.ProofPages);
            Assert.Equal(original.Select(item => item.ProofId), remote.Submissions.Select(item => item.ProofId));
            Assert.Equal(original.Select(item => item.RecoveryIntentKey), remote.Submissions.Select(item => item.Key));
            Assert.Equal(original.Select(item => item.ProofId), store.Finalizations);
            Assert.All(store.Records.Take(65), item => Assert.Equal(StatefulCharge(item.ProofId), item.FinalizedChargeId));
            Assert.Null(store.Records[65].FinalizedChargeId);
            store.ProofPages.Clear();
            using (var fresh = StatefulCoordinator(remote, bridge, store))
                await fresh.RecoverAsync(Owner, CancellationToken.None);
            Assert.Equal(new[] { (0L, 32L, 66L), (32L, 64L, 66L), (64L, 66L, 66L) }, store.ProofPages);
            Assert.Equal(store.Records.Select(item => item.ProofId), remote.Submissions.Select(item => item.ProofId));
            Assert.Equal(store.Records.Select(item => item.RecoveryIntentKey), remote.Submissions.Select(item => item.Key));
            Assert.Equal(66, store.Finalizations.Distinct().Count());
            Assert.Equal(66, store.Finalizations.Count);
            Assert.All(store.Records, item => Assert.Equal(StatefulCharge(item.ProofId), item.FinalizedChargeId));
            Assert.DoesNotContain("launch", steps);
        }
        [Fact]
        public async Task CancelledSecondProofPageResumesRetainedIdentitiesWithoutResubmittingFinalizedProofs()
        {
            var steps = new List<string>();
            var store = new StatefulProofStore();
            for (int i = 1; i <= 65; i++)
                store.SeedProof(i);
            var original = store.Records.ToArray();
            var remote = new StatefulRecoveryRemote(store) { HoldSubmission = 33 };
            var bridge = new TestBridge(steps);
            using var caller = new CancellationTokenSource();
            using (var first = StatefulCoordinator(remote, bridge, store))
            {
                var operation = first.RecoverAsync(Owner, caller.Token);
                try
                {
                    await remote.SubmissionStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    Assert.Equal(32, store.Finalizations.Count);
                    Assert.Equal(original[32].ProofId, remote.Submissions[32].ProofId);
                    caller.Cancel();
                    remote.SubmissionRelease.SetResult(true);
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
                }
                finally
                {
                    caller.Cancel();
                    remote.SubmissionRelease.TrySetResult(true);
                }
            }
            Assert.Equal(new[] { (0L, 32L, 65L), (32L, 64L, 65L) }, store.ProofPages);
            Assert.All(store.Records.Take(32), item => Assert.NotNull(item.FinalizedChargeId));
            Assert.All(store.Records.Skip(32), item => Assert.Null(item.FinalizedChargeId));
            var resumed = new StatefulRecoveryRemote(store);
            using (var fresh = StatefulCoordinator(resumed, bridge, store))
                await fresh.RecoverAsync(Owner, CancellationToken.None);
            Assert.Equal(original.Skip(32).Select(item => item.ProofId), resumed.Submissions.Select(item => item.ProofId));
            Assert.Equal(original.Skip(32).Select(item => item.RecoveryIntentKey), resumed.Submissions.Select(item => item.Key));
            Assert.Equal(original.Select(item => item.ProofId), store.Finalizations);
            Assert.All(store.Records, item => Assert.Equal(StatefulCharge(item.ProofId), item.FinalizedChargeId));
            Assert.DoesNotContain("launch", steps);
        }
        [Theory]
        [InlineData("high-water")]
        [InlineData("after-sequence")]
        [InlineData("repeated-cursor")]
        public async Task ProofRecoveryRejectsInconsistentSecondPageBeforeSubmission(string fault)
        {
            var store = new StatefulProofStore { PageFault = fault };
            for (int i = 1; i <= 65; i++)
                store.SeedProof(i);
            var remote = new StatefulRecoveryRemote(store);
            using var coordinator = StatefulCoordinator(remote, new TestBridge(new List<string>()), store);
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RecoverAsync(Owner, CancellationToken.None));
            Assert.Equal(2, store.ProofPages.Count);
            Assert.Equal(store.Records.Take(32).Select(item => item.ProofId), remote.Submissions.Select(item => item.ProofId));
            Assert.Equal(32, store.Finalizations.Count);
            Assert.All(store.Records.Skip(32), item => Assert.Null(item.FinalizedChargeId));
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RetainedPurchaseProofUsesPurchaseIntentAnd409FallbackKeepsRecoveryIdentity(bool conflict)
        {
            var steps = new List<string>();
            var store = new StatefulProofStore();
            var original = store.SeedReservedProof(1);
            var attempt = store.Attempts[original.ReservedAttemptKey!.Value];
            var remote = new RetainedPurchaseRemote(store) { SubmitStatusCode = conflict ? 409 : 0 };
            using var coordinator = StatefulCoordinator(remote, new TestBridge(steps), store);
            await coordinator.RecoverAsync(Owner, CancellationToken.None);
            await coordinator.RecoverAsync(Owner, CancellationToken.None);
            Assert.Equal(conflict ? new[] { "purchase", "recovery" } : new[] { "purchase" }, remote.Calls.Select(call => call.Route));
            Assert.Equal(attempt.Intent!.IntentId, remote.Calls[0].Identity);
            Assert.All(remote.Calls, call => Assert.Equal(original.ProofId, call.ProofId));
            if (conflict)
            {
                Assert.Equal(original.RecoveryIntentKey, remote.Calls[1].Identity);
                Assert.Same(attempt, store.Attempts[attempt.IntentKey]);
            }
            else
            {
                Assert.Same(Assert.Single(remote.ReturnedStates), store.Attempts[attempt.IntentKey].Intent);
                Assert.True(store.Attempts[attempt.IntentKey].LaunchRequested);
            }
            var finalized = Assert.Single(store.Records);
            Assert.Equal(original.ProofId, finalized.ProofId);
            Assert.Equal(original.RecoveryIntentKey, finalized.RecoveryIntentKey);
            Assert.Equal(original.ReservedAttemptKey, finalized.ReservedAttemptKey);
            Assert.Equal(StatefulCharge(original.ProofId), finalized.FinalizedChargeId);
            Assert.Equal(original.ProofId, Assert.Single(store.Finalizations));
            Assert.Single(remote.ReturnedStates);
            Assert.DoesNotContain("launch", steps);
        }
        [Fact]
        public async Task Interrupted409FallbackResumesRetainedPurchaseAndRecoveryWithoutAnotherSheet()
        {
            var steps = new List<string>();
            var store = new StatefulProofStore();
            var original = store.SeedReservedProof(1);
            var attempt = store.Attempts[original.ReservedAttemptKey!.Value];
            var proof = store.Proofs[original.ProofId];
            var interrupted = new RetainedPurchaseRemote(store) { HoldRecovery = true };
            var bridge = new TestBridge(steps);
            using var caller = new CancellationTokenSource();
            using (var first = StatefulCoordinator(interrupted, bridge, store))
            {
                var operation = first.RecoverAsync(Owner, caller.Token);
                try
                {
                    await interrupted.RecoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    Assert.Equal(new[] { "purchase", "recovery" }, interrupted.Calls.Select(call => call.Route));
                    Assert.Equal(attempt.Intent!.IntentId, interrupted.Calls[0].Identity);
                    Assert.Equal(original.RecoveryIntentKey, interrupted.Calls[1].Identity);
                    Assert.All(interrupted.Calls, call => Assert.Equal(original.ProofId, call.ProofId));
                    Assert.Empty(interrupted.ReturnedStates);
                    Assert.Empty(store.Finalizations);
                    caller.Cancel();
                    interrupted.RecoveryRelease.SetResult(true);
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(30)));
                }
                finally
                {
                    caller.Cancel();
                    interrupted.RecoveryRelease.TrySetResult(true);
                }
            }
            Assert.Same(original, Assert.Single(store.Records));
            Assert.Same(attempt, store.Attempts[attempt.IntentKey]);
            Assert.Same(proof, store.Proofs[original.ProofId]);
            Assert.Null(original.FinalizedChargeId);
            var resumed = new RetainedPurchaseRemote(store);
            using (var fresh = StatefulCoordinator(resumed, bridge, store))
            {
                await fresh.RecoverAsync(Owner, CancellationToken.None);
                await fresh.RecoverAsync(Owner, CancellationToken.None);
            }
            Assert.Equal(new[] { "purchase", "recovery" }, resumed.Calls.Select(call => call.Route));
            Assert.Equal(attempt.Intent!.IntentId, resumed.Calls[0].Identity);
            Assert.Equal(original.RecoveryIntentKey, resumed.Calls[1].Identity);
            Assert.All(resumed.Calls, call => Assert.Equal(original.ProofId, call.ProofId));
            Assert.Single(resumed.ReturnedStates);
            Assert.Equal(original.ProofId, Assert.Single(store.Finalizations));
            Assert.Equal(StatefulCharge(original.ProofId), Assert.Single(store.Records).FinalizedChargeId);
            Assert.Same(attempt, store.Attempts[attempt.IntentKey]);
            Assert.DoesNotContain("launch", steps);
        }
        [Theory]
        [InlineData(400)]
        [InlineData(503)]
        [InlineData(-1)]
        public async Task RetainedPurchaseNon409FailureDoesNotEnterRecoveryFallback(int statusCode)
        {
            var steps = new List<string>();
            var store = new StatefulProofStore();
            var original = store.SeedReservedProof(1);
            var attempt = store.Attempts[original.ReservedAttemptKey!.Value];
            var proof = store.Proofs[original.ProofId];
            var remote = new RetainedPurchaseRemote(store) { SubmitStatusCode = statusCode };
            using var coordinator = StatefulCoordinator(remote, new TestBridge(steps), store);
            var error = await Assert.ThrowsAsync<NativeRecoveryException>(() => coordinator.RecoverAsync(Owner, CancellationToken.None));
            Assert.Equal(1, error.UnresolvedCount);
            var call = Assert.Single(remote.Calls);
            Assert.Equal("purchase", call.Route);
            Assert.Equal(attempt.Intent!.IntentId, call.Identity);
            Assert.Equal(original.ProofId, call.ProofId);
            Assert.Equal(statusCode, remote.ThrownSubmitStatusCode);
            Assert.Empty(remote.ReturnedStates);
            Assert.Same(original, Assert.Single(store.Records));
            Assert.Same(attempt, store.Attempts[attempt.IntentKey]);
            Assert.Same(proof, store.Proofs[original.ProofId]);
            Assert.Null(original.FinalizedChargeId);
            Assert.Empty(store.Finalizations);
            Assert.DoesNotContain("launch", steps);
        }
        [Theory]
        [InlineData(false, "key")]
        [InlineData(false, "source")]
        [InlineData(false, "package")]
        [InlineData(false, "store-product")]
        [InlineData(false, "intent-id")]
        [InlineData(false, "launch")]
        [InlineData(true, "key")]
        [InlineData(true, "source")]
        [InlineData(true, "package")]
        [InlineData(true, "store-product")]
        [InlineData(true, "launch")]
        public async Task RetainedPurchaseRejectsInvalidResponseIdentityBeforeCommitOrFinalization(bool conflict, string fault)
        {
            var steps = new List<string>();
            var store = new StatefulProofStore();
            var original = store.SeedReservedProof(1);
            var attempt = store.Attempts[original.ReservedAttemptKey!.Value];
            var proof = store.Proofs[original.ProofId];
            var remote = new RetainedPurchaseRemote(store) { SubmitStatusCode = conflict ? 409 : 0, ResponseFault = fault };
            using var coordinator = StatefulCoordinator(remote, new TestBridge(steps), store);
            var error = await Assert.ThrowsAsync<NativeRecoveryException>(() => coordinator.RecoverAsync(Owner, CancellationToken.None));
            Assert.Equal(1, error.UnresolvedCount);
            Assert.Equal(conflict ? new[] { "purchase", "recovery" } : new[] { "purchase" }, remote.Calls.Select(call => call.Route));
            Assert.Equal(attempt.Intent!.IntentId, remote.Calls[0].Identity);
            Assert.All(remote.Calls, call => Assert.Equal(original.ProofId, call.ProofId));
            if (conflict)
                Assert.Equal(original.RecoveryIntentKey, remote.Calls[1].Identity);
            var returned = Assert.Single(remote.ReturnedStates);
            Assert.Equal("granted", returned.State);
            Assert.Equal("finished", returned.FinishState);
            Assert.Equal(StatefulCharge(original.ProofId), returned.ChargeId);
            switch (fault)
            {
                case "key":
                    Assert.NotEqual(conflict ? original.RecoveryIntentKey : attempt.IntentKey, returned.IntentKey);
                    break;
                case "source":
                    Assert.NotEqual(conflict ? "recovery" : "purchase", returned.Source);
                    break;
                case "package":
                    Assert.NotEqual(original.PackageName, returned.PackageName);
                    break;
                case "store-product":
                    Assert.NotEqual(original.StoreProductId, returned.StoreProductId);
                    break;
                case "intent-id":
                    Assert.NotEqual(attempt.Intent.IntentId, returned.IntentId);
                    break;
                case "launch":
                    Assert.True(returned.LaunchAllowed);
                    break;
                default:
                    throw new InvalidOperationException("Unknown response fault.");
            }
            Assert.Same(original, Assert.Single(store.Records));
            Assert.Same(attempt, store.Attempts[attempt.IntentKey]);
            Assert.Same(proof, store.Proofs[original.ProofId]);
            Assert.Null(original.FinalizedChargeId);
            Assert.Empty(store.Finalizations);
            Assert.DoesNotContain("launch", steps);
        }
        [Theory]
        [InlineData("wrong-intent")]
        [InlineData("ungranted")]
        [InlineData("missing-revision")]
        [InlineData("missing-visibility")]
        [InlineData("missing-epoch")]
        [InlineData("missing-grant")]
        [InlineData("absent-checkpoint")]
        [InlineData("stale-revision")]
        [InlineData("wrong-visibility")]
        [InlineData("wrong-epoch")]
        public async Task PresentationRejectsIncompleteGrantOrCheckpointWithoutClaim(string fault)
        {
            var store = new StatefulProofStore();
            var remote = new CoverageRemote { StatusFactory = _ => CoverageState(1, fault: fault) };
            var checkpoint = new Checkpoint { Value = PresentationCheckpoint(fault) };
            using var coordinator = CoverageCoordinator(remote, store, checkpoint);
            Assert.False(await coordinator.TryClaimPresentationAsync(Owner, StatefulId(1), CancellationToken.None));
            Assert.Equal(0, store.ClaimCalls);
            Assert.Empty(store.PresentationClaims);
            Assert.Empty(store.Records);
            Assert.Empty(store.Attempts);
        }
        [Fact]
        public async Task ExactCheckpointClaimsPresentationOnceAcrossCoordinators()
        {
            var store = new StatefulProofStore();
            var remote = new CoverageRemote();
            var checkpoint = new Checkpoint { Value = PresentationCheckpoint("") };
            using (var first = CoverageCoordinator(remote, store, checkpoint))
                Assert.True(await first.TryClaimPresentationAsync(Owner, StatefulId(1), CancellationToken.None));
            using (var fresh = CoverageCoordinator(remote, store, checkpoint))
                Assert.False(await fresh.TryClaimPresentationAsync(Owner, StatefulId(1), CancellationToken.None));
            Assert.Equal(StatefulId(4001), Assert.Single(store.PresentationClaims));
            Assert.Equal(2, store.ClaimCalls);
            Assert.Empty(store.Finalizations);
        }
        [Theory]
        [InlineData("absent-checkpoint")]
        [InlineData("stale-revision")]
        [InlineData("wrong-visibility")]
        [InlineData("wrong-epoch")]
        public async Task SecondCheckpointInvalidationRejectsPresentationAfterClaimWasConsumed(string fault)
        {
            var store = new StatefulProofStore { HoldClaim = true };
            var checkpoint = new Checkpoint { Value = PresentationCheckpoint("") };
            using var coordinator = CoverageCoordinator(new CoverageRemote(), store, checkpoint);
            var operation = coordinator.TryClaimPresentationAsync(Owner, StatefulId(1), CancellationToken.None);
            try
            {
                await store.ClaimCommitted.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Equal(StatefulId(4001), Assert.Single(store.PresentationClaims));
                checkpoint.Value = PresentationCheckpoint(fault);
                store.ClaimRelease.TrySetResult(true);
                Assert.False(await operation.WaitAsync(TimeSpan.FromSeconds(30)));
                checkpoint.Value = PresentationCheckpoint("");
                Assert.False(await coordinator.TryClaimPresentationAsync(Owner, StatefulId(1), CancellationToken.None));
                Assert.Equal(StatefulId(4001), Assert.Single(store.PresentationClaims));
                Assert.Equal(2, store.ClaimCalls);
            }
            finally { store.ClaimRelease.TrySetResult(true); }
        }
        [Fact]
        public async Task ResumeAttemptsUpdatesMetadataAcrossCapturedPagesWithoutLaunchingOrIncludingAppend()
        {
            var store = new StatefulProofStore();
            for (int i = 1; i <= 65; i++)
                store.Attempts.Add(StatefulId(i), new NativeAttempt(StatefulId(i), Product,
                    i % 2 == 0 ? CoverageState(i) : null, i % 2 == 0));
            var original = store.Attempts.Values.ToArray();
            var remote = new CoverageRemote
            {
                CreateFactory = key => CoverageState(StatefulProofNumber(key), state: "pending_verification"),
                StatusFactory = id => CoverageState(StatefulProofNumber(id), state: "pending_verification")
            };
            store.OnIntentCommit = count =>
            {
                if (count == 1)
                    store.Attempts.Add(StatefulId(66), new NativeAttempt(StatefulId(66), Product, null, false));
            };
            var steps = new List<string>();
            using var coordinator = StatefulCoordinator(remote, new TestBridge(steps), store);
            await coordinator.ResumeAttemptsAsync(Owner, CancellationToken.None);
            Assert.Equal(new[] { (0L, 32L, 65L), (32L, 64L, 65L), (64L, 65L, 65L) }, store.AttemptPages);
            Assert.Equal(original.Where(item => item.Intent == null).Select(item => item.IntentKey), remote.Created);
            Assert.Equal(original.Where(item => item.Intent != null).Select(item => item.Intent!.IntentId), remote.StatusReads);
            Assert.Equal(original.Select(item => item.IntentKey), store.IntentCommits);
            foreach (var old in original)
            {
                var updated = store.Attempts[old.IntentKey];
                Assert.Same(old.Product, updated.Product);
                Assert.Equal(old.LaunchRequested, updated.LaunchRequested);
                Assert.Equal(old.IntentKey, updated.Intent!.IntentKey);
                Assert.Equal(old.Intent?.IntentId ?? old.IntentKey, updated.Intent.IntentId);
                Assert.Equal("pending_verification", updated.Intent.State);
            }
            Assert.Null(store.Attempts[StatefulId(66)].Intent);
            Assert.Empty(store.Records);
            Assert.DoesNotContain("launch", steps);
        }
        [Theory]
        [InlineData("wrong-intent")]
        [InlineData("key")]
        [InlineData("product")]
        [InlineData("version")]
        [InlineData("package")]
        [InlineData("sku")]
        [InlineData("environment")]
        [InlineData("type")]
        [InlineData("binding")]
        [InlineData("source")]
        public async Task ResumeAttemptsRejectsChangedIdentityWithoutOverwritingRetainedAttempt(string fault)
        {
            var store = new StatefulProofStore();
            var original = new NativeAttempt(StatefulId(1), Product, CoverageState(1), true);
            store.Attempts.Add(original.IntentKey, original);
            var remote = new CoverageRemote { StatusFactory = _ => CoverageState(1, fault: fault) };
            using var coordinator = CoverageCoordinator(remote, store);
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ResumeAttemptsAsync(Owner, CancellationToken.None));
            Assert.Same(original, Assert.Single(store.Attempts.Values));
            Assert.Empty(store.IntentCommits);
            Assert.Empty(store.Records);
            Assert.Equal(original.Intent!.IntentId, Assert.Single(remote.StatusReads));
        }
        [Fact]
        public async Task FindAttemptRejectsStoreReturningAnotherIdentity()
        {
            var store = new StatefulProofStore { WrongFindIdentity = true };
            var original = new NativeAttempt(StatefulId(1), Product, CoverageState(1), true);
            store.Attempts.Add(original.IntentKey, original);
            using var coordinator = CoverageCoordinator(new CoverageRemote(), store);
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.FindAttemptAsync(Owner, original.IntentKey, CancellationToken.None));
            Assert.Same(original, Assert.Single(store.Attempts.Values));
            Assert.Empty(store.IntentCommits);
        }
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OpenedProofScopeMismatchRetainsOriginalWithoutSubmissionOrFinalization(bool changePackage)
        {
            var store = new StatefulProofStore();
            var original = store.SeedReservedProof(1);
            var attempt = Assert.Single(store.Attempts.Values);
            store.Proofs[original.ProofId] = new NativeProof(changePackage ? "other.package" : Product.PackageName,
                changePackage ? Product.StoreProductId : "other.sku", "synthetic-1");
            var remote = new RetainedPurchaseRemote(store);
            using var coordinator = StatefulCoordinator(remote, new TestBridge(new List<string>()), store);
            var error = await Assert.ThrowsAsync<NativeRecoveryException>(() => coordinator.RecoverAsync(Owner, CancellationToken.None));
            Assert.Equal(1, error.UnresolvedCount);
            Assert.Same(original, Assert.Single(store.Records));
            Assert.Same(attempt, Assert.Single(store.Attempts.Values));
            Assert.Empty(remote.Calls);
            Assert.Empty(store.IntentCommits);
            Assert.Empty(store.Finalizations);
        }
        [Fact]
        public async Task StatefulTokenReplayKeepsFinalizedTombstoneAndDifferentTokenHasDistinctIdentity()
        {
            var store = new StatefulProofStore();
            store.SeedProof(1);
            var original = Assert.Single(store.Records);
            var proof = store.Proofs[original.ProofId];
            var remote = new StatefulRecoveryRemote(store);
            var bridge = new TestBridge(new List<string>()) { ReturnUnfinished = true, ProofToken = "synthetic-1" };
            using (var first = StatefulCoordinator(remote, bridge, store))
                await first.RecoverAsync(Owner, CancellationToken.None);
            var tombstone = Assert.Single(store.Records);
            Assert.Equal(StatefulCharge(original.ProofId), tombstone.FinalizedChargeId);
            using (var replay = StatefulCoordinator(remote, bridge, store))
                await replay.RecoverAsync(Owner, CancellationToken.None);
            Assert.Same(tombstone, Assert.Single(store.Records));
            Assert.Same(proof, store.Proofs[original.ProofId]);
            Assert.Equal(original.ProofId, Assert.Single(store.OpenedProofs));
            Assert.Equal((original.RecoveryIntentKey, original.ProofId), Assert.Single(remote.Submissions));
            bridge.ProofToken = "synthetic-2";
            using (var different = StatefulCoordinator(remote, bridge, store))
                await different.RecoverAsync(Owner, CancellationToken.None);
            Assert.Equal(2, store.Records.Count);
            var next = store.Records[1];
            Assert.Same(tombstone, store.Records[0]);
            Assert.NotEqual(original.ProofId, next.ProofId);
            Assert.NotEqual(original.RecoveryIntentKey, next.RecoveryIntentKey);
            Assert.Equal("synthetic-2", store.Proofs[next.ProofId].Token);
            Assert.Null(next.ReservedAttemptKey);
            Assert.Equal(StatefulCharge(next.ProofId), next.FinalizedChargeId);
            Assert.Equal(new[] { original.ProofId, next.ProofId }, store.OpenedProofs);
            Assert.Equal(new[] { original.ProofId, next.ProofId }, store.Finalizations);
            Assert.Equal(new[] { original.RecoveryIntentKey, next.RecoveryIntentKey }, remote.Submissions.Select(item => item.Key));
        }
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task MismatchedLaunchCallbackIsRetainedWithoutAttributionAndReturnsUnresolved(bool continuePrepared, bool changePackage)
        {
            var steps = new List<string>();
            var store = new StatefulProofStore();
            var bridge = new TestBridge(steps)
            {
                ProofPackage = changePackage ? "other.package" : Product.PackageName,
                ProofSku = changePackage ? Product.StoreProductId : "other.sku"
            };
            var remote = new TestRemote(steps) { UseProofScopeForRecovery = true };
            using var coordinator = StatefulCoordinator(remote, bridge, store);
            Guid? prepared = null;
            if (continuePrepared)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.StartAsync(Owner, Product,
                    new NativeStoreProduct(Product.PackageName, Product.StoreProductId, "$0.99"), CancellationToken.None));
                prepared = Assert.Single(store.Attempts.Values).IntentKey;
            }
            var outcome = continuePrepared
                ? await coordinator.ContinuePreparedAsync(Owner, prepared!.Value, Price, CancellationToken.None)
                : await coordinator.StartAsync(Owner, Product, Price, CancellationToken.None);
            Assert.Equal(NativeLaunchStatus.Unresolved, outcome.Status);
            var retained = Assert.Single(store.Records);
            Assert.Equal(bridge.ProofPackage, retained.PackageName);
            Assert.Equal(bridge.ProofSku, retained.StoreProductId);
            Assert.Equal(bridge.ProofPackage, store.Proofs[retained.ProofId].PackageName);
            Assert.Equal(bridge.ProofSku, store.Proofs[retained.ProofId].StoreProductId);
            Assert.Equal("synthetic", store.Proofs[retained.ProofId].Token);
            Assert.Null(retained.ReservedAttemptKey);
            Assert.Null(retained.FinalizedChargeId);
            var attempt = Assert.Single(store.Attempts.Values);
            Assert.Equal(outcome.IntentKey, attempt.IntentKey);
            Assert.True(attempt.LaunchRequested);
            Assert.Equal("awaiting_proof", attempt.Intent!.State);
            Assert.Null(attempt.Intent.GrantId);
            Assert.Single(store.IntentCommits);
            Assert.Single(store.OpenedProofs);
            Assert.Empty(store.Finalizations);
            Assert.Single(steps, step => step == "submit");
            Assert.Single(steps, step => step == "launch");
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RetiredLaunchRetainsReturnedProofAndQueuedResumeCancelsBeforeFreshRecovery(bool continuePrepared)
        {
            var steps = new List<string>();
            var store = new StatefulProofStore { HoldProtection = true };
            var bridge = new TestBridge(steps) { HoldLaunch = true };
            using (var first = StatefulCoordinator(new TestRemote(steps), bridge, store))
            {
                Guid? prepared = null;
                if (continuePrepared)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => first.StartAsync(Owner, Product,
                        new NativeStoreProduct(Product.PackageName, Product.StoreProductId, "$0.99"), CancellationToken.None));
                    prepared = Assert.Single(store.Attempts.Values).IntentKey;
                }
                var operation = continuePrepared
                    ? first.ContinuePreparedAsync(Owner, prepared!.Value, Price, CancellationToken.None)
                    : first.StartAsync(Owner, Product, Price, CancellationToken.None);
                try
                {
                    await bridge.LaunchStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    var queued = first.ResumeAttemptsAsync(Owner, CancellationToken.None);
                    Assert.False(queued.IsCompleted);
                    await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => first.Dispose()))).WaitAsync(TimeSpan.FromSeconds(30));
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(TimeSpan.FromSeconds(30)));
                    bridge.LaunchRelease.TrySetResult(true);
                    await store.ProtectionCommitted.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    Assert.Equal(CancellationToken.None, store.ProtectionToken);
                    var retained = Assert.Single(store.Records);
                    Assert.Null(retained.ReservedAttemptKey);
                    Assert.Null(retained.FinalizedChargeId);
                    Assert.Equal("synthetic", store.Proofs[retained.ProofId].Token);
                    Assert.False(operation.IsCompleted);
                    store.ProtectionRelease.TrySetResult(true);
                    await Assert.ThrowsAsync<ObjectDisposedException>(() => operation.WaitAsync(TimeSpan.FromSeconds(30)));
                    await Assert.ThrowsAsync<ObjectDisposedException>(() => first.ResumeAttemptsAsync(Owner, CancellationToken.None));
                    Assert.True(Assert.Single(store.Attempts.Values).LaunchRequested);
                    Assert.Single(store.IntentCommits);
                    Assert.DoesNotContain("submit", steps);
                }
                finally
                {
                    first.Dispose();
                    bridge.LaunchRelease.TrySetResult(true);
                    store.ProtectionRelease.TrySetResult(true);
                }
            }
            var original = Assert.Single(store.Records);
            var remote = new StatefulRecoveryRemote(store);
            using (var fresh = StatefulCoordinator(remote, bridge, store))
                await fresh.RecoverAsync(Owner, CancellationToken.None);
            Assert.Equal((original.RecoveryIntentKey, original.ProofId), Assert.Single(remote.Submissions));
            Assert.Equal(original.ProofId, Assert.Single(store.Finalizations));
            Assert.Equal(StatefulCharge(original.ProofId), Assert.Single(store.Records).FinalizedChargeId);
            Assert.Single(steps, step => step == "launch");
        }
        [Fact]
        public async Task RetirementRejectsLateSuccessfulRemoteResultIgnoringCancellation()
        {
            var store = new StatefulProofStore();
            var remote = new CoverageRemote { HoldStatus = true };
            using var coordinator = CoverageCoordinator(remote, store, new Checkpoint { Value = PresentationCheckpoint("") });
            var operation = coordinator.TryClaimPresentationAsync(Owner, StatefulId(1), CancellationToken.None);
            try
            {
                await remote.StatusStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
                coordinator.Dispose();
                Assert.True(remote.StatusToken.IsCancellationRequested);
                remote.StatusRelease.TrySetResult(true);
                await Assert.ThrowsAsync<ObjectDisposedException>(() => operation.WaitAsync(TimeSpan.FromSeconds(30)));
                Assert.Empty(store.PresentationClaims);
                Assert.Equal(0, store.ClaimCalls);
            }
            finally { remote.StatusRelease.TrySetResult(true); }
        }
        [Theory]
        [InlineData("proof")]
        [InlineData("recovery")]
        [InlineData("reservation")]
        public void UnfinalizedRecordRejectsEmptyRequiredOrOptionalIdentity(string field)
        {
            Assert.Throws<ArgumentException>(() => new ProtectedNativeProofRecord(field == "proof" ? Guid.Empty : StatefulId(1),
                field == "recovery" ? Guid.Empty : StatefulId(2), Product.PackageName, Product.StoreProductId,
                field == "reservation" ? Guid.Empty : StatefulId(3)));
        }
        [Theory]
        [InlineData(true, "null")]
        [InlineData(false, "null")]
        [InlineData(true, "empty")]
        [InlineData(false, "empty")]
        [InlineData(true, "ascii-overflow")]
        [InlineData(false, "ascii-overflow")]
        [InlineData(true, "utf8-overflow")]
        [InlineData(false, "utf8-overflow")]
        [InlineData(true, "high-surrogate")]
        [InlineData(false, "high-surrogate")]
        [InlineData(true, "low-surrogate")]
        [InlineData(false, "low-surrogate")]
        public void UnfinalizedRecordRejectsMissingOversizedOrMalformedScope(bool package, string fault)
        {
            string? value = fault switch
            {
                "null" => null,
                "empty" => "",
                "ascii-overflow" => new string('a', 257),
                "utf8-overflow" => new string('\u00e9', 129),
                "high-surrogate" => "\ud800",
                "low-surrogate" => "\udc00",
                _ => throw new InvalidOperationException()
            };
            Assert.Throws<ArgumentException>(() => new ProtectedNativeProofRecord(StatefulId(1), StatefulId(2),
                package ? value! : Product.PackageName, package ? Product.StoreProductId : value!));
        }
        [Theory]
        [InlineData("whitespace")]
        [InlineData("ascii-boundary")]
        [InlineData("utf8-boundary")]
        public void UnfinalizedRecordPreservesNativeScopeAndExactNonV7IssuedIdentities(string boundary)
        {
            var proof = Guid.Parse("11111111-1111-4111-8111-111111111111");
            var recovery = Guid.Parse("22222222-2222-4222-8222-222222222222");
            var reserved = Guid.Parse("33333333-3333-4333-8333-333333333333");
            string scope = boundary == "whitespace" ? " \t " : boundary == "ascii-boundary" ? new string('a', 256) : new string('\u00e9', 128);
            var record = new ProtectedNativeProofRecord(proof, recovery, scope, scope, reserved);
            Assert.Equal(proof, record.ProofId);
            Assert.Equal(recovery, record.RecoveryIntentKey);
            Assert.Equal(reserved, record.ReservedAttemptKey);
            Assert.Equal(scope, record.PackageName);
            Assert.Equal(scope, record.StoreProductId);
            Assert.Null(record.FinalizedChargeId);
            Assert.Null(new ProtectedNativeProofRecord(proof, recovery, scope, scope).ReservedAttemptKey);
        }
        [Theory]
        [InlineData("proof")]
        [InlineData("recovery")]
        [InlineData("reservation")]
        [InlineData("charge")]
        [InlineData("package-blank")]
        [InlineData("sku-blank")]
        [InlineData("package-length")]
        [InlineData("sku-length")]
        public void FinalizedRecordRetainsItsPreexistingStrictValidation(string fault)
        {
            var nonV7 = Guid.Parse("11111111-1111-4111-8111-111111111111");
            Assert.Throws<ArgumentException>(() => new ProtectedNativeProofRecord(fault == "proof" ? nonV7 : StatefulId(1),
                fault == "recovery" ? nonV7 : StatefulId(2), fault == "package-blank" ? " " : fault == "package-length" ? new string('a', 256) : Product.PackageName,
                fault == "sku-blank" ? " " : fault == "sku-length" ? new string('a', 256) : Product.StoreProductId,
                fault == "reservation" ? nonV7 : StatefulId(3), fault == "charge" ? nonV7 : StatefulId(4)));
        }
        [Fact]
        public void FinalizedRecordKeepsCharacterBasedMultibyteBoundary()
        {
            var scope = new string('\u00e9', 255);
            var record = new ProtectedNativeProofRecord(StatefulId(1), StatefulId(2), scope, scope, StatefulId(3), StatefulId(4));
            Assert.Equal(scope, record.PackageName);
            Assert.Equal(scope, record.StoreProductId);
            Assert.Equal(StatefulId(4), record.FinalizedChargeId);
        }
        [Fact]
        public async Task HistoryReadsFollowRemoteCursorAndPreserveReturnedStates()
        {
            var first = CoverageState(1);
            var second = CoverageState(2);
            var remote = new CoverageRemote
            {
                History = (after, _) => after == null
                ? new NativeRecoveryPage(new[] { first }, first.IntentId) : new NativeRecoveryPage(new[] { second }, null)
            };
            using var coordinator = CoverageCoordinator(remote, new StatefulProofStore());
            var result = await coordinator.ReadPurchasesAsync(Owner, CancellationToken.None);
            Assert.Equal(new[] { first, second }, result);
            Assert.Equal(new (Guid?, int)[] { (null, 50), (first.IntentId, 50) }, remote.HistoryReads);
        }
        [Theory]
        [InlineData("duplicate")]
        [InlineData("oversized")]
        [InlineData("empty-cursor")]
        [InlineData("foreign-cursor")]
        [InlineData("repeated-cursor")]
        [InlineData("over-200")]
        [InlineData("200-with-cursor")]
        public async Task HistoryReadsRejectMalformedCursorOrUnboundedMaterialization(string fault)
        {
            int call = 0;
            var remote = new CoverageRemote
            {
                History = (after, _) =>
                {
                    call++;
                    if (fault == "duplicate")
                        return new NativeRecoveryPage(new[] { CoverageState(1), CoverageState(1) }, null);
                    if (fault == "oversized")
                        return new NativeRecoveryPage(Enumerable.Range(1, 51).Select(i => CoverageState(i)).ToArray(), null);
                    if (fault == "empty-cursor")
                        return new NativeRecoveryPage(Array.Empty<NativeIntentState>(), StatefulId(1));
                    if (fault == "foreign-cursor")
                        return new NativeRecoveryPage(new[] { CoverageState(1) }, StatefulId(2));
                    if (fault == "repeated-cursor")
                        return call == 1
                        ? new NativeRecoveryPage(new[] { CoverageState(1) }, StatefulId(1))
                        : new NativeRecoveryPage(new[] { CoverageState(2) }, after);
                    int count = fault == "over-200" ? (call == 5 ? 5 : 49) : 50;
                    var items = Enumerable.Range((call - 1) * 50 + 1, count).Select(i => CoverageState(i)).ToArray();
                    return new NativeRecoveryPage(items, fault == "over-200" && call == 5 ? null : items[count - 1].IntentId);
                }
            };
            using var coordinator = CoverageCoordinator(remote, new StatefulProofStore());
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ReadPurchasesAsync(Owner, CancellationToken.None));
            Assert.Equal(fault == "repeated-cursor" ? 2 : fault == "over-200" ? 5 : fault == "200-with-cursor" ? 4 : 1, remote.HistoryReads.Count);
        }
        [Theory]
        [InlineData("null-page")]
        [InlineData("null-items")]
        [InlineData("null-item")]
        [InlineData("oversized")]
        [InlineData("empty-cursor")]
        [InlineData("cursor-not-last")]
        [InlineData("duplicate")]
        [InlineData("unordered")]
        [InlineData("before-after")]
        public async Task ExplicitHistoryPageRejectsMalformedResultsBeforeReturning(string fault)
        {
            var remote = new CoverageRemote
            {
                History = (_, _) => fault switch
                {
                    "null-page" => null!,
                    "null-items" => new NativeRecoveryPage(null!, null),
                    "null-item" => new NativeRecoveryPage(new NativeIntentState[] { null! }, null),
                    "oversized" => new NativeRecoveryPage(new[] { CoverageState(2), CoverageState(3), CoverageState(4) }, null),
                    "empty-cursor" => new NativeRecoveryPage(Array.Empty<NativeIntentState>(), StatefulId(2)),
                    "cursor-not-last" => new NativeRecoveryPage(new[] { CoverageState(2), CoverageState(3) }, StatefulId(2)),
                    "duplicate" => new NativeRecoveryPage(new[] { CoverageState(2), CoverageState(2) }, null),
                    "unordered" => new NativeRecoveryPage(new[] { CoverageState(3), CoverageState(2) }, null),
                    "before-after" => new NativeRecoveryPage(new[] { CoverageState(1) }, null),
                    _ => throw new InvalidOperationException()
                }
            };
            using var coordinator = CoverageCoordinator(remote, new StatefulProofStore());
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ReadPurchasePageAsync(Owner, StatefulId(1), 2, CancellationToken.None));
            Assert.Equal(((Guid?)StatefulId(1), 2), Assert.Single(remote.HistoryReads));
        }
        [Fact]
        public async Task ExplicitHistoryPagePassesExactCursorAndLimitAndRejectsInvalidLimitsLocally()
        {
            var page = new NativeRecoveryPage(new[] { CoverageState(2), CoverageState(3) }, StatefulId(3));
            var remote = new CoverageRemote { History = (_, _) => page };
            using var coordinator = CoverageCoordinator(remote, new StatefulProofStore());
            Assert.Same(page, await coordinator.ReadPurchasePageAsync(Owner, StatefulId(1), 2, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => coordinator.ReadPurchasePageAsync(Owner, null, 0, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => coordinator.ReadPurchasePageAsync(Owner, null, 51, CancellationToken.None));
            Assert.Equal(((Guid?)StatefulId(1), 2), Assert.Single(remote.HistoryReads));
        }
        private static NativeInstalledCheckpoint? PresentationCheckpoint(string fault) => fault == "absent-checkpoint" ? null
            : new NativeInstalledCheckpoint(fault == "stale-revision" ? 41 : 42, fault == "wrong-visibility" ? 3 : 2,
                fault == "wrong-epoch" ? StatefulId(999) : StatefulId(5001));
        private static NativeIntentState CoverageState(int index, string fault = "", string state = "granted") =>
            new NativeIntentState(StatefulId(fault == "wrong-intent" ? 999 : index), StatefulId(fault == "key" ? 999 : index),
                fault == "product" ? "other.product" : Product.ProductId, fault == "version" ? 2 : Product.Version,
                fault == "package" ? "other.package" : Product.PackageName, fault == "sku" ? "other.sku" : Product.StoreProductId,
                fault == "environment" ? "other" : Product.Environment, StatefulId(fault == "binding" ? 999 : 5000),
                fault == "source" ? "recovery" : "purchase", false, fault == "ungranted" ? "pending_verification" : state, null, "pending",
                fault == "missing-revision" ? null : 42, fault == "missing-visibility" ? null : 2,
                fault == "missing-epoch" ? null : StatefulId(5001), fault == "missing-grant" ? null : StatefulId(4001), StatefulId(2001),
                productType: fault == "type" ? "non_consumable" : "consumable");
        private static NativePurchaseCoordinator CoverageCoordinator(INativePurchaseRemote remote, StatefulProofStore store, Checkpoint? checkpoint = null) =>
            new NativePurchaseCoordinator(Owner, remote, new TestBridge(new List<string>()), store, checkpoint ?? new Checkpoint(),
                new UuidV7Generator(new Clock(), new DeterministicRandom()));
        private sealed class CoverageRemote : INativePurchaseRemote
        {
            public Func<Guid, NativeIntentState> StatusFactory = id => CoverageState(StatefulProofNumber(id));
            public Func<Guid, NativeIntentState>? CreateFactory;
            public Func<Guid?, int, NativeRecoveryPage>? History;
            public readonly List<Guid> Created = new List<Guid>();
            public readonly List<Guid> StatusReads = new List<Guid>();
            public readonly List<(Guid? After, int Limit)> HistoryReads = new List<(Guid?, int)>();
            public bool HoldStatus;
            public CancellationToken StatusToken;
            public readonly TaskCompletionSource<bool> StatusStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> StatusRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public async Task<NativeIntentState> StatusAsync(Guid id, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                StatusReads.Add(id);
                StatusToken = token;
                if (HoldStatus)
                {
                    StatusStarted.TrySetResult(true);
                    await StatusRelease.Task; // Deliberately models a remote adapter that ignores cancellation.
                }
                return StatusFactory(id);
            }
            public Task<NativeIntentState> CreateIntentAsync(Guid key, string productId, int version, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Assert.Equal(Product.ProductId, productId);
                Assert.Equal(Product.Version, version);
                Created.Add(key);
                return Task.FromResult((CreateFactory ?? throw new InvalidOperationException("Intent creation is outside this fixture."))(key));
            }
            public Task<NativeRecoveryPage> RecoveryAsync(Guid? after, int limit, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                HistoryReads.Add((after, limit));
                return Task.FromResult((History ?? throw new InvalidOperationException("History is outside this fixture."))(after, limit));
            }
            public Task<IReadOnlyList<NativeProduct>> ProductsAsync(CancellationToken token) => throw new InvalidOperationException();
            public Task<NativeIntentState> SubmitProofAsync(Guid id, NativeProof proof, CancellationToken token) => throw new InvalidOperationException();
            public Task<NativeIntentState> RecoverProofAsync(Guid key, NativeProof proof, CancellationToken token) => throw new InvalidOperationException();
        }
        private static NativePurchaseCoordinator StatefulCoordinator(INativePurchaseRemote remote, INativeStoreBridge bridge, StatefulProofStore store) =>
            new NativePurchaseCoordinator(Owner, remote, bridge, store, new Checkpoint(),
                new UuidV7Generator(new Clock(), new DeterministicRandom()));
        private static Guid StatefulId(int value) => Guid.Parse("0199f9a0-0000-7000-8000-" + value.ToString("x12"));
        private static int StatefulProofNumber(Guid proofId) => int.Parse(proofId.ToString("D").Substring(24), System.Globalization.NumberStyles.HexNumber);
        private static Guid StatefulCharge(Guid proofId) => StatefulId(2000 + StatefulProofNumber(proofId));
        private sealed class DeterministicRandom : IUuidRandomSource
        {
            private byte next;
            public void Fill(Span<byte> destination) { destination.Clear(); destination[destination.Length - 1] = ++next; }
        }
        // Models retained state across coordinator instances only; not host encryption or disk durability.
        private sealed class StatefulProofStore : IPagedProtectedProofStore
        {
            public readonly Dictionary<Guid, NativeAttempt> Attempts = new Dictionary<Guid, NativeAttempt>();
            public readonly List<ProtectedNativeProofRecord> Records = new List<ProtectedNativeProofRecord>();
            public readonly Dictionary<Guid, NativeProof> Proofs = new Dictionary<Guid, NativeProof>();
            public readonly List<Guid> Finalizations = new List<Guid>();
            public readonly List<(long After, long Last, long High)> ProofPages = new List<(long, long, long)>();
            public readonly List<(long After, long Last, long High)> AttemptPages = new List<(long, long, long)>();
            public readonly List<Guid> IntentCommits = new List<Guid>();
            public readonly HashSet<Guid> PresentationClaims = new HashSet<Guid>();
            public readonly List<Guid> OpenedProofs = new List<Guid>();
            public Action<int>? OnIntentCommit;
            public bool WrongFindIdentity;
            public bool HoldClaim;
            public int ClaimCalls;
            public readonly TaskCompletionSource<bool> ClaimCommitted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> ClaimRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool HoldProtection;
            public string? PageFault;
            public CancellationToken ProtectionToken;
            public readonly TaskCompletionSource<bool> ProtectionCommitted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> ProtectionRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public void SeedProof(int index)
            {
                var proof = new NativeProof(Product.PackageName, Product.StoreProductId, "synthetic-" + index);
                AddProof(index, proof, StatefulId(1000 + index), null);
            }
            public ProtectedNativeProofRecord SeedReservedProof(int index)
            {
                var key = StatefulId(6000 + index);
                var intent = new NativeIntentState(StatefulId(7000 + index), key, Product.ProductId, Product.Version,
                    Product.PackageName, Product.StoreProductId, Product.Environment, StatefulId(5000), "purchase", false,
                    "pending_verification", null, "not_required", null, null, null, null, null);
                Attempts.Add(key, new NativeAttempt(key, Product, intent, true));
                return AddProof(index, new NativeProof(Product.PackageName, Product.StoreProductId, "synthetic-" + index),
                    StatefulId(1000 + index), key);
            }
            private ProtectedNativeProofRecord AddProof(int index, NativeProof proof, Guid recoveryKey, Guid? reservedKey)
            {
                var record = new ProtectedNativeProofRecord(StatefulId(index), recoveryKey, proof.PackageName, proof.StoreProductId, reservedKey);
                Records.Add(record);
                Proofs.Add(record.ProofId, proof);
                return record;
            }
            private static void Check(ScopedOwnerContext owner, CancellationToken token)
            { Assert.Equal(Owner, owner); token.ThrowIfCancellationRequested(); }
            public Task ReserveAttemptAsync(ScopedOwnerContext owner, Guid key, NativeProduct product, int maximumProofBytes, CancellationToken token)
            { Check(owner, token); Assert.Equal(8192, maximumProofBytes); Attempts.Add(key, new NativeAttempt(key, product, null, false)); return Task.CompletedTask; }
            public Task CommitIntentAsync(ScopedOwnerContext owner, Guid key, NativeIntentState intent, CancellationToken token)
            {
                Check(owner, token);
                var old = Attempts[key];
                Attempts[key] = new NativeAttempt(key, old.Product, intent, old.LaunchRequested);
                IntentCommits.Add(key);
                OnIntentCommit?.Invoke(IntentCommits.Count);
                return Task.CompletedTask;
            }
            public Task MarkLaunchRequestedAsync(ScopedOwnerContext owner, Guid key, CancellationToken token)
            { Check(owner, token); var old = Attempts[key]; Attempts[key] = new NativeAttempt(key, old.Product, old.Intent, true); return Task.CompletedTask; }
            public Task<IReadOnlyList<NativeAttempt>> ReadAttemptsAsync(ScopedOwnerContext owner, CancellationToken token) =>
                throw new InvalidOperationException("Legacy attempt materialization is forbidden.");
            public Task<IReadOnlyList<ProtectedNativeProofRecord>> ReadProtectedRecordsAsync(ScopedOwnerContext owner, CancellationToken token) =>
                throw new InvalidOperationException("Legacy proof materialization is forbidden.");
            public Task<NativeAttempt?> FindAttemptAsync(ScopedOwnerContext owner, Guid key, CancellationToken token)
            {
                Check(owner, token);
                if (WrongFindIdentity)
                    return Task.FromResult<NativeAttempt?>(new NativeAttempt(StatefulId(999), Product, null, false));
                return Task.FromResult(Attempts.TryGetValue(key, out var attempt) ? attempt : null);
            }
            public Task<NativeProtectedPage<NativeAttempt>> ReadAttemptPageAsync(ScopedOwnerContext owner, string? cursor, int limit, CancellationToken token)
            {
                Check(owner, token);
                Assert.Equal(32, limit);
                int after = cursor == null ? 0 : int.Parse(cursor.Split(':')[0]);
                int high = cursor == null ? Attempts.Count : int.Parse(cursor.Split(':')[1]);
                int last = Math.Min(after + limit, high);
                AttemptPages.Add((after, last, high));
                return Task.FromResult(new NativeProtectedPage<NativeAttempt>(Attempts.Values.Skip(after).Take(last - after).ToArray(),
                    last < high ? last + ":" + high : null, high, after, last));
            }
            public async Task<ProtectedNativeProofRecord> ProtectAndCommitAsync(ScopedOwnerContext owner, NativeProof proof, Guid proposedKey, Guid? reservedKey, CancellationToken token)
            {
                Check(owner, token);
                ProtectionToken = token;
                var existing = Records.SingleOrDefault(item => item.PackageName == proof.PackageName && item.StoreProductId == proof.StoreProductId && Proofs[item.ProofId].Token == proof.Token);
                var record = existing ?? AddProof(Records.Count + 1, proof, proposedKey, reservedKey);
                ProtectionCommitted.TrySetResult(true);
                if (HoldProtection)
                    await ProtectionRelease.Task;
                return record;
            }
            public Task<NativeProof> OpenForSubmissionAsync(ScopedOwnerContext owner, Guid proofId, CancellationToken token)
            { Check(owner, token); Assert.Null(Records.Single(item => item.ProofId == proofId).FinalizedChargeId); OpenedProofs.Add(proofId); return Task.FromResult(Proofs[proofId]); }
            public Task MarkFinalizedAsync(ScopedOwnerContext owner, Guid proofId, Guid chargeId, CancellationToken token)
            {
                Check(owner, token);
                int index = Records.FindIndex(item => item.ProofId == proofId);
                var old = Records[index];
                Assert.Null(old.FinalizedChargeId);
                Assert.Equal(StatefulCharge(proofId), chargeId);
                Records[index] = new ProtectedNativeProofRecord(old.ProofId, old.RecoveryIntentKey, old.PackageName, old.StoreProductId, old.ReservedAttemptKey, chargeId);
                Finalizations.Add(proofId);
                return Task.CompletedTask;
            }
            public async Task<bool> TryClaimPresentationAsync(ScopedOwnerContext owner, Guid grantId, CancellationToken token)
            {
                Check(owner, token);
                ClaimCalls++;
                if (!PresentationClaims.Add(grantId))
                    return false;
                ClaimCommitted.TrySetResult(true);
                if (HoldClaim)
                    await ClaimRelease.Task;
                token.ThrowIfCancellationRequested();
                return true;
            }
            public Task<NativeProtectedPage<ProtectedNativeProofRecord>> ReadProofPageAsync(ScopedOwnerContext owner, string? cursor, int limit, CancellationToken token)
            {
                Check(owner, token);
                Assert.Equal(32, limit);
                int after = cursor == null ? 0 : int.Parse(cursor.Split(':')[0]);
                int high = cursor == null ? Records.Count : int.Parse(cursor.Split(':')[1]);
                int last = Math.Min(after + limit, high);
                string? next = last < high ? last + ":" + high : null;
                var items = Records.Skip(after).Take(last - after).ToArray();
                if (cursor != null)
                {
                    if (PageFault == "high-water")
                        high++;
                    if (PageFault == "after-sequence")
                        after--;
                    if (PageFault == "repeated-cursor")
                        next = cursor;
                }
                ProofPages.Add((after, last, high));
                return Task.FromResult(new NativeProtectedPage<ProtectedNativeProofRecord>(items, next, high, after, last));
            }
        }
        private sealed class RetainedPurchaseRemote : INativePurchaseRemote
        {
            private readonly StatefulProofStore store;
            public readonly List<(string Route, Guid Identity, Guid ProofId)> Calls = new List<(string, Guid, Guid)>();
            public readonly List<NativeIntentState> ReturnedStates = new List<NativeIntentState>();
            public int SubmitStatusCode = 409;
            public int? ThrownSubmitStatusCode;
            public string? ResponseFault;
            public bool HoldRecovery;
            public readonly TaskCompletionSource<bool> RecoveryStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> RecoveryRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public RetainedPurchaseRemote(StatefulProofStore store) => this.store = store;
            public Task<NativeIntentState> SubmitProofAsync(Guid intentId, NativeProof proof, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var attempt = store.Attempts.Values.Single(item => item.Intent!.IntentId == intentId);
                var record = store.Records.Single(item => item.ReservedAttemptKey == attempt.IntentKey);
                CheckProof(record, proof);
                Calls.Add(("purchase", intentId, record.ProofId));
                if (SubmitStatusCode != 0)
                {
                    ThrownSubmitStatusCode = SubmitStatusCode;
                    if (SubmitStatusCode == -1)
                        throw new InvalidOperationException("Synthetic non-remote submission failure.");
                    throw new NativePurchaseRemoteException(SubmitStatusCode);
                }
                return Task.FromResult(Response(record, false));
            }
            public async Task<NativeIntentState> RecoverProofAsync(Guid key, NativeProof proof, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var record = store.Records.Single(item => item.RecoveryIntentKey == key);
                CheckProof(record, proof);
                Calls.Add(("recovery", key, record.ProofId));
                if (HoldRecovery)
                {
                    RecoveryStarted.SetResult(true);
                    await RecoveryRelease.Task;
                }
                token.ThrowIfCancellationRequested();
                return Response(record, true);
            }
            private void CheckProof(ProtectedNativeProofRecord record, NativeProof proof)
            {
                Assert.Same(store.Proofs[record.ProofId], proof);
                Assert.Equal(record.PackageName, proof.PackageName);
                Assert.Equal(record.StoreProductId, proof.StoreProductId);
                Assert.Equal("synthetic-" + StatefulProofNumber(record.ProofId), proof.Token);
            }
            private NativeIntentState Response(ProtectedNativeProofRecord record, bool recovery)
            {
                var attempt = store.Attempts[record.ReservedAttemptKey!.Value];
                var state = new NativeIntentState(ResponseFault == "intent-id" ? StatefulId(9001) : recovery ? StatefulId(3001) : attempt.Intent!.IntentId,
                    ResponseFault == "key" ? StatefulId(9002) : recovery ? record.RecoveryIntentKey : attempt.IntentKey,
                    Product.ProductId, Product.Version, ResponseFault == "package" ? "other.package" : record.PackageName,
                    ResponseFault == "store-product" ? "other.sku" : record.StoreProductId, Product.Environment, StatefulId(5000),
                    ResponseFault == "source" ? (recovery ? "purchase" : "recovery") : (recovery ? "recovery" : "purchase"),
                    ResponseFault == "launch", "granted", null, "finished", 1, 1, StatefulId(5001), StatefulId(4001), StatefulCharge(record.ProofId));
                ReturnedStates.Add(state);
                return state;
            }
            public Task<IReadOnlyList<NativeProduct>> ProductsAsync(CancellationToken token) => throw new InvalidOperationException();
            public Task<NativeIntentState> CreateIntentAsync(Guid key, string productId, int version, CancellationToken token) => throw new InvalidOperationException("Retained proof must not allocate a purchase intent.");
            public Task<NativeIntentState> StatusAsync(Guid intentId, CancellationToken token) => throw new InvalidOperationException();
            public Task<NativeRecoveryPage> RecoveryAsync(Guid? after, int limit, CancellationToken token) => throw new InvalidOperationException();
        }
        private sealed class StatefulRecoveryRemote : INativePurchaseRemote
        {
            private readonly StatefulProofStore store;
            public readonly List<(Guid Key, Guid ProofId)> Submissions = new List<(Guid, Guid)>();
            public Action<int>? OnSubmission;
            public int HoldSubmission;
            public readonly TaskCompletionSource<bool> SubmissionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> SubmissionRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public StatefulRecoveryRemote(StatefulProofStore store) => this.store = store;
            public async Task<NativeIntentState> RecoverProofAsync(Guid key, NativeProof proof, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var record = store.Records.Single(item => item.RecoveryIntentKey == key);
                Assert.Same(store.Proofs[record.ProofId], proof);
                Assert.Equal(record.PackageName, proof.PackageName);
                Assert.Equal(record.StoreProductId, proof.StoreProductId);
                Submissions.Add((key, record.ProofId));
                OnSubmission?.Invoke(Submissions.Count);
                if (Submissions.Count == HoldSubmission)
                {
                    SubmissionStarted.SetResult(true);
                    await SubmissionRelease.Task;
                }
                token.ThrowIfCancellationRequested();
                return new NativeIntentState(StatefulId(3000 + StatefulProofNumber(record.ProofId)), key, Product.ProductId, Product.Version,
                    proof.PackageName, proof.StoreProductId, Product.Environment, StatefulId(5000), "recovery", false,
                    "granted", null, "finished", 1, 1, StatefulId(5001), StatefulId(4000 + StatefulProofNumber(record.ProofId)), StatefulCharge(record.ProofId));
            }
            public Task<IReadOnlyList<NativeProduct>> ProductsAsync(CancellationToken token) => throw new InvalidOperationException();
            public Task<NativeIntentState> CreateIntentAsync(Guid key, string productId, int version, CancellationToken token) => throw new InvalidOperationException("Recovery must not allocate a purchase intent.");
            public Task<NativeIntentState> SubmitProofAsync(Guid intentId, NativeProof proof, CancellationToken token) => throw new InvalidOperationException("Unattributed proof must use recovery.");
            public Task<NativeIntentState> StatusAsync(Guid intentId, CancellationToken token) => throw new InvalidOperationException();
            public Task<NativeRecoveryPage> RecoveryAsync(Guid? after, int limit, CancellationToken token) => throw new InvalidOperationException();
        }
        private static NativePurchaseCoordinator Create(List<string> steps, TestStore store) =>
            new NativePurchaseCoordinator(Owner, new TestRemote(steps), new TestBridge(steps), store, new Checkpoint(),
                new UuidV7Generator(new Clock(), new CryptographicUuidRandomSource()));
        private sealed class Clock : IUnixMillisecondClock { public long GetUnixMilliseconds() => 1_789_555_200_000; }
        private sealed class Checkpoint : INativeInstalledCheckpointReader
        {
            public NativeInstalledCheckpoint? Value;
            public Task<NativeInstalledCheckpoint?> ReadAsync(ScopedOwnerContext owner, CancellationToken token) => Task.FromResult(Value);
        }
        private sealed class TestBridge : INativeStoreBridge
        {
            private readonly List<string> steps;
            public bool ReturnUnfinished;
            public bool ReturnPending;
            public bool PendingWithProof;
            public bool HoldLaunch;
            public string ProofToken = "synthetic";
            public string ProofPackage = "test.package";
            public string ProofSku = "test.sku";
            public readonly TaskCompletionSource<bool> LaunchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> LaunchRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TestBridge(List<string> steps) => this.steps = steps;
            public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<IReadOnlyList<NativeStoreProduct>> QueryProductsAsync(IReadOnlyList<NativeProduct> products, CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<NativeStoreProduct>>(new[] { new NativeStoreProduct("test.package", "test.sku", "$1.00") });
            public async Task<NativeLaunchResult> LaunchAsync(NativeLaunchRequest request, CancellationToken cancellationToken)
            { steps.Add("launch"); LaunchStarted.TrySetResult(true); if (HoldLaunch) await LaunchRelease.Task; return PendingWithProof ? new NativeLaunchResult(NativeLaunchStatus.Pending, new NativeProof(ProofPackage, ProofSku, ProofToken)) : ReturnPending ? new NativeLaunchResult(NativeLaunchStatus.Pending) : new NativeLaunchResult(NativeLaunchStatus.Proof, new NativeProof(ProofPackage, ProofSku, ProofToken)); }
            public int ScanUnresolved;
            public Task<NativeProofScanResult> QueryUnfinishedAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new NativeProofScanResult(ReturnUnfinished ? new[] { new NativeProof("test.package", "test.sku", ProofToken) } : Array.Empty<NativeProof>(), ScanUnresolved));
        }
        private sealed class TestRemote : INativePurchaseRemote
        {
            private readonly List<string> steps;
            public NativeIntentState? Status;
            public bool FailFirstRecovery;
            public bool GrantOnSubmit;
            public bool UseProofScopeForRecovery;
            private Guid purchaseKey;
            public TestRemote(List<string> steps) => this.steps = steps;
            public Task<IReadOnlyList<NativeProduct>> ProductsAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<NativeProduct>>(new[] { Product });
            public Task<NativeIntentState> CreateIntentAsync(Guid key, string productId, int version, CancellationToken token)
            { steps.Add("intent"); purchaseKey = key; Status = new NativeIntentState(Guid.Parse("0199f9a0-0000-7000-8000-000000000003"), key, productId, version, Product.PackageName, Product.StoreProductId, Product.Environment, Guid.Parse("0199f9a0-0000-7000-8000-000000000004"), "purchase", true, "awaiting_proof", null, "not_required", null, null, null, null, null); return Task.FromResult(Status); }
            public Task<NativeIntentState> RecoverProofAsync(Guid intentKey, NativeProof proof, CancellationToken token)
            { steps.Add("submit"); if (FailFirstRecovery) { FailFirstRecovery = false; throw new InvalidOperationException(); } return Task.FromResult(new NativeIntentState(Guid.Parse("0199f9a0-0000-7000-8000-000000000005"), intentKey, Product.ProductId, Product.Version, UseProofScopeForRecovery ? proof.PackageName : Product.PackageName, UseProofScopeForRecovery ? proof.StoreProductId : Product.StoreProductId, Product.Environment, Guid.Parse("0199f9a0-0000-7000-8000-000000000004"), "recovery", false, "pending_verification", null, "not_required", null, null, null, null, null)); }
            public Task<NativeIntentState> StatusAsync(Guid intentId, CancellationToken token) => Task.FromResult(Status ?? throw new NotImplementedException());
            public Task<NativeRecoveryPage> RecoveryAsync(Guid? afterIntentId, int limit, CancellationToken token) => throw new NotImplementedException();
            public Task<NativeIntentState> SubmitProofAsync(Guid intentId, NativeProof proof, CancellationToken token)
            { steps.Add("submit"); return Task.FromResult(new NativeIntentState(intentId, purchaseKey, Product.ProductId, Product.Version, Product.PackageName, Product.StoreProductId, Product.Environment, Guid.Parse("0199f9a0-0000-7000-8000-000000000004"), "purchase", false, GrantOnSubmit ? "granted" : "pending_verification", null, GrantOnSubmit ? "pending" : "not_required", GrantOnSubmit ? 1 : (long?)null, GrantOnSubmit ? 1 : (long?)null, GrantOnSubmit ? Guid.Parse("0199f9a0-0000-7000-8000-000000000005") : (Guid?)null, GrantOnSubmit ? Guid.Parse("0199f9a0-0000-7000-8000-000000000006") : (Guid?)null, GrantOnSubmit ? Guid.Parse("0199f9a0-0000-7000-8000-000000000007") : (Guid?)null)); }
        }
        private class TestStore : IBoundedProtectedProofStore
        {
            private readonly List<string> steps;
            public int MaximumRecords => 256;
            public bool FailReserve;
            public bool ThrowOnOpen;
            private readonly List<NativeAttempt> attempts = new List<NativeAttempt>();
            public void Seed(NativeAttempt value) => attempts.Add(value);
            public readonly List<ProtectedNativeProofRecord> Records = new List<ProtectedNativeProofRecord>();
            public TestStore(List<string> steps) => this.steps = steps;
            public Task ReserveAttemptAsync(ScopedOwnerContext owner, Guid intentKey, NativeProduct product, int maximumProofBytes, CancellationToken token)
            { steps.Add("reserve"); if (FailReserve) throw new InvalidOperationException(); attempts.Add(new NativeAttempt(intentKey, product, null, false)); return Task.CompletedTask; }
            public Task CommitIntentAsync(ScopedOwnerContext owner, Guid intentKey, NativeIntentState intent, CancellationToken token)
            { steps.Add("commit"); var index = attempts.FindIndex(value => value.IntentKey == intentKey); attempts[index] = new NativeAttempt(intentKey, attempts[index].Product, intent, attempts[index].LaunchRequested); return Task.CompletedTask; }
            public Task MarkLaunchRequestedAsync(ScopedOwnerContext owner, Guid intentKey, CancellationToken token)
            { steps.Add("launch-marker"); var index = attempts.FindIndex(value => value.IntentKey == intentKey); attempts[index] = new NativeAttempt(intentKey, attempts[index].Product, attempts[index].Intent, true); return Task.CompletedTask; }
            public virtual Task<IReadOnlyList<NativeAttempt>> ReadAttemptsAsync(ScopedOwnerContext owner, CancellationToken token) =>
                Task.FromResult<IReadOnlyList<NativeAttempt>>(attempts.ToArray());
            public Task<ProtectedNativeProofRecord> ProtectAndCommitAsync(ScopedOwnerContext owner, NativeProof proof, Guid key, Guid? reservedAttemptKey, CancellationToken token)
            {
                steps.Add("protect");
                if (Records.Count != 0)
                    return Task.FromResult(Records[0]);
                var record = new ProtectedNativeProofRecord(Guid.NewGuid(), key, proof.PackageName, proof.StoreProductId, reservedAttemptKey);
                Records.Add(record);
                return Task.FromResult(record);
            }
            public Task<IReadOnlyList<ProtectedNativeProofRecord>> ReadProtectedRecordsAsync(ScopedOwnerContext owner, CancellationToken token) => Task.FromResult<IReadOnlyList<ProtectedNativeProofRecord>>(Records);
            public Task<NativeProof> OpenForSubmissionAsync(ScopedOwnerContext owner, Guid proofId, CancellationToken token) =>
                ThrowOnOpen ? throw new InvalidOperationException("Compacted proof must not open.") : Task.FromResult(new NativeProof("test.package", "test.sku", "synthetic"));
            public Task MarkFinalizedAsync(ScopedOwnerContext owner, Guid proofId, Guid chargeId, CancellationToken token) => Task.CompletedTask;
            public Task<bool> TryClaimPresentationAsync(ScopedOwnerContext owner, Guid grantId, CancellationToken token) => Task.FromResult(true);
        }
        private sealed class PagedTestStore : TestStore, IPagedProtectedProofStore
        {
            public readonly List<NativeAttempt> PagedAttempts = new List<NativeAttempt>();
            public int PageCalls;
            public bool Drift;
            public PagedTestStore(List<string> steps) : base(steps) { }
            public override Task<IReadOnlyList<NativeAttempt>> ReadAttemptsAsync(ScopedOwnerContext owner, CancellationToken token) => throw new InvalidOperationException("Legacy materialization is forbidden.");
            public Task<NativeAttempt?> FindAttemptAsync(ScopedOwnerContext owner, Guid key, CancellationToken token) => Task.FromResult(PagedAttempts.SingleOrDefault(item => item.IntentKey == key));
            public Task<NativeProtectedPage<NativeAttempt>> ReadAttemptPageAsync(ScopedOwnerContext owner, string? cursor, int limit, CancellationToken token)
            {
                PageCalls++;
                int after = cursor == null ? 0 : int.Parse(cursor.Split(':')[0]);
                int high = cursor == null ? PagedAttempts.Count : int.Parse(cursor.Split(':')[1]);
                if (Drift && cursor != null)
                    high++;
                int last = Math.Min(after + limit, high);
                return Task.FromResult(new NativeProtectedPage<NativeAttempt>(PagedAttempts.Skip(after).Take(last - after).ToArray(),
                    last < high ? last + ":" + high : null, high, after, last));
            }
            public Task<NativeProtectedPage<ProtectedNativeProofRecord>> ReadProofPageAsync(ScopedOwnerContext owner, string? cursor, int limit, CancellationToken token) =>
                Task.FromResult(new NativeProtectedPage<ProtectedNativeProofRecord>(Array.Empty<ProtectedNativeProofRecord>(), null, 0, 0, 0));
        }
    }
}
