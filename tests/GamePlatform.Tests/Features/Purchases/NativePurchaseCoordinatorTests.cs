using System;
using System.Collections.Generic;
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
            Assert.Single(store.Records);
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
            public readonly TaskCompletionSource<bool> LaunchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> LaunchRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TestBridge(List<string> steps) => this.steps = steps;
            public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task<IReadOnlyList<NativeStoreProduct>> QueryProductsAsync(IReadOnlyList<NativeProduct> products, CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<NativeStoreProduct>>(new[] { new NativeStoreProduct("test.package", "test.sku", "$1.00") });
            public async Task<NativeLaunchResult> LaunchAsync(NativeLaunchRequest request, CancellationToken cancellationToken)
            { steps.Add("launch"); LaunchStarted.TrySetResult(true); if (HoldLaunch) await LaunchRelease.Task; return PendingWithProof ? new NativeLaunchResult(NativeLaunchStatus.Pending, new NativeProof("test.package", "test.sku", "synthetic")) : ReturnPending ? new NativeLaunchResult(NativeLaunchStatus.Pending) : new NativeLaunchResult(NativeLaunchStatus.Proof, new NativeProof("test.package", "test.sku", "synthetic")); }
            public int ScanUnresolved;
            public Task<NativeProofScanResult> QueryUnfinishedAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new NativeProofScanResult(ReturnUnfinished ? new[] { new NativeProof("test.package", "test.sku", "synthetic") } : Array.Empty<NativeProof>(), ScanUnresolved));
        }
        private sealed class TestRemote : INativePurchaseRemote
        {
            private readonly List<string> steps;
            public NativeIntentState? Status;
            public bool FailFirstRecovery;
            public bool GrantOnSubmit;
            private Guid purchaseKey;
            public TestRemote(List<string> steps) => this.steps = steps;
            public Task<IReadOnlyList<NativeProduct>> ProductsAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<NativeProduct>>(new[] { Product });
            public Task<NativeIntentState> CreateIntentAsync(Guid key, string productId, int version, CancellationToken token)
            { steps.Add("intent"); purchaseKey = key; Status = new NativeIntentState(Guid.Parse("0199f9a0-0000-7000-8000-000000000003"), key, productId, version, Product.PackageName, Product.StoreProductId, Product.Environment, Guid.Parse("0199f9a0-0000-7000-8000-000000000004"), "purchase", true, "awaiting_proof", null, "not_required", null, null, null, null, null); return Task.FromResult(Status); }
            public Task<NativeIntentState> RecoverProofAsync(Guid intentKey, NativeProof proof, CancellationToken token)
            { steps.Add("submit"); if (FailFirstRecovery) { FailFirstRecovery = false; throw new InvalidOperationException(); } return Task.FromResult(new NativeIntentState(Guid.Parse("0199f9a0-0000-7000-8000-000000000005"), intentKey, Product.ProductId, Product.Version, Product.PackageName, Product.StoreProductId, Product.Environment, Guid.Parse("0199f9a0-0000-7000-8000-000000000004"), "recovery", false, "pending_verification", null, "not_required", null, null, null, null, null)); }
            public Task<NativeIntentState> StatusAsync(Guid intentId, CancellationToken token) => Task.FromResult(Status ?? throw new NotImplementedException());
            public Task<NativeRecoveryPage> RecoveryAsync(Guid? afterIntentId, int limit, CancellationToken token) => throw new NotImplementedException();
            public Task<NativeIntentState> SubmitProofAsync(Guid intentId, NativeProof proof, CancellationToken token)
            { steps.Add("submit"); return Task.FromResult(new NativeIntentState(intentId, purchaseKey, Product.ProductId, Product.Version, Product.PackageName, Product.StoreProductId, Product.Environment, Guid.Parse("0199f9a0-0000-7000-8000-000000000004"), "purchase", false, GrantOnSubmit ? "granted" : "pending_verification", null, GrantOnSubmit ? "pending" : "not_required", GrantOnSubmit ? 1 : (long?)null, GrantOnSubmit ? 1 : (long?)null, GrantOnSubmit ? Guid.Parse("0199f9a0-0000-7000-8000-000000000005") : (Guid?)null, GrantOnSubmit ? Guid.Parse("0199f9a0-0000-7000-8000-000000000006") : (Guid?)null, GrantOnSubmit ? Guid.Parse("0199f9a0-0000-7000-8000-000000000007") : (Guid?)null)); }
        }
        private sealed class TestStore : IProtectedProofStore
        {
            private readonly List<string> steps;
            public bool FailReserve;
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
            public Task<IReadOnlyList<NativeAttempt>> ReadAttemptsAsync(ScopedOwnerContext owner, CancellationToken token) =>
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
                Task.FromResult(new NativeProof("test.package", "test.sku", "synthetic"));
            public Task MarkFinalizedAsync(ScopedOwnerContext owner, Guid proofId, Guid chargeId, CancellationToken token) => Task.CompletedTask;
            public Task<bool> TryClaimPresentationAsync(ScopedOwnerContext owner, Guid grantId, CancellationToken token) => Task.FromResult(true);
        }
    }
}
