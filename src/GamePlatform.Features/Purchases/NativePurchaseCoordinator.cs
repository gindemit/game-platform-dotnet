using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions.Sync;
namespace GamePlatform.Features.Purchases
{
    /// <summary>Coordinates protected native proof recovery. Only the server grants value.</summary>
    public sealed class NativePurchaseCoordinator : IDisposable
    {
        private readonly ScopedOwnerContext owner;
        private readonly INativePurchaseRemote remote;
        private readonly INativeStoreBridge bridge;
        private readonly IProtectedProofStore store;
        private readonly INativeInstalledCheckpointReader checkpoints;
        private readonly UuidV7Generator ids;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        private bool disposed;
        public NativePurchaseCoordinator(ScopedOwnerContext owner, INativePurchaseRemote remote, INativeStoreBridge bridge,
            IProtectedProofStore store, INativeInstalledCheckpointReader checkpoints, UuidV7Generator ids)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            this.owner = owner;
            this.remote = remote ?? throw new ArgumentNullException(nameof(remote));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
            this.ids = ids ?? throw new ArgumentNullException(nameof(ids));
        }
        public async Task<IReadOnlyList<NativeProduct>> ProductsAsync(ScopedOwnerContext requested, CancellationToken token)
        {
            using var linked = Link(requested, token);
            var result = await remote.ProductsAsync(linked.Token).ConfigureAwait(false);
            Ensure(requested);
            linked.Token.ThrowIfCancellationRequested();
            return result;
        }
        public async Task<IReadOnlyList<NativeStoreProduct>> StoreProductsAsync(ScopedOwnerContext requested,
            IReadOnlyList<NativeProduct> products, CancellationToken token)
        {
            if (products == null)
                throw new ArgumentNullException(nameof(products));
            using var linked = Link(requested, token);
            await bridge.ConnectAsync(linked.Token).ConfigureAwait(false);
            Ensure(requested);
            var result = await bridge.QueryProductsAsync(products, linked.Token).ConfigureAwait(false);
            Ensure(requested);
            linked.Token.ThrowIfCancellationRequested();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var product in result)
            {
                if (product == null || string.IsNullOrWhiteSpace(product.DisplayPrice) ||
                    !products.Any(item => item.PackageName == product.PackageName && item.StoreProductId == product.StoreProductId) ||
                    !seen.Add(product.PackageName + "\n" + product.StoreProductId))
                    throw new InvalidOperationException("Invalid native product details.");
            }
            return Array.AsReadOnly(result.ToArray());
        }
        public async Task<IReadOnlyList<NativeIntentState>> ReadPurchasesAsync(ScopedOwnerContext requested, CancellationToken token)
        {
            using var linked = Link(requested, token);
            var results = new List<NativeIntentState>();
            Guid? after = null;
            var seen = new HashSet<Guid>();
            do
            {
                var page = await remote.RecoveryAsync(after, 50, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                if (page.Intents.Count > 50)
                    throw new InvalidOperationException("Native recovery page exceeds its bound.");
                foreach (var item in page.Intents)
                    if (!seen.Add(item.IntentId))
                        throw new InvalidOperationException("Native recovery repeats an intent.");
                results.AddRange(page.Intents);
                if (page.NextIntentId.HasValue && (page.Intents.Count == 0 || page.NextIntentId == after || !seen.Contains(page.NextIntentId.Value)))
                    throw new InvalidOperationException("Native recovery cursor did not advance.");
                after = page.NextIntentId;
            } while (after.HasValue);
            return results.AsReadOnly();
        }
        public async Task<NativeStartOutcome> StartAsync(ScopedOwnerContext requested, NativeProduct product, NativeStoreProduct confirmedPrice, CancellationToken token,
            bool acknowledgeUnresolvedPriorPurchase = false)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (confirmedPrice == null)
                throw new ArgumentNullException(nameof(confirmedPrice));
            using var linked = Link(requested, token);
            if (!await serial.WaitAsync(0, linked.Token).ConfigureAwait(false))
                throw new InvalidOperationException("A native purchase operation is already in progress.");
            try
            {
                Ensure(requested);
                var attempts = await store.ReadAttemptsAsync(owner, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                if (!acknowledgeUnresolvedPriorPurchase && attempts.Any(a => a.Product.PackageName == product.PackageName && a.Product.StoreProductId == product.StoreProductId &&
                    (a.Intent == null || (a.Intent.State != "granted" && a.Intent.State != "rejected"))))
                    throw new InvalidOperationException("A prior native purchase remains unresolved.");
                var intentKey = ids.NewId();
                await store.ReserveAttemptAsync(owner, intentKey, product, 8192, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                var intent = await remote.CreateIntentAsync(intentKey, product.ProductId, product.Version, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                if (intent.IntentKey != intentKey || intent.ProductId != product.ProductId || intent.ProductVersion != product.Version ||
                    intent.PackageName != product.PackageName || intent.StoreProductId != product.StoreProductId ||
                    intent.Environment != product.Environment || intent.Source != "purchase" || !intent.LaunchAllowed || intent.State != "awaiting_proof")
                    throw new InvalidOperationException("Native intent does not authorize this launch.");
                await store.CommitIntentAsync(owner, intentKey, intent, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                await bridge.ConnectAsync(linked.Token).ConfigureAwait(false);
                Ensure(requested);
                var details = await bridge.QueryProductsAsync(new[] { product }, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                var matching = details.Where(p => p.PackageName == product.PackageName && p.StoreProductId == product.StoreProductId &&
                    !string.IsNullOrWhiteSpace(p.DisplayPrice)).ToArray();
                if (matching.Length != 1 || confirmedPrice.PackageName != product.PackageName ||
                    confirmedPrice.StoreProductId != product.StoreProductId || confirmedPrice.DisplayPrice != matching[0].DisplayPrice)
                    throw new InvalidOperationException("Fresh native product details are unavailable.");
                await store.MarkLaunchRequestedAsync(owner, intentKey, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                var result = await bridge.LaunchAsync(new NativeLaunchRequest(intent.PackageName, intent.StoreProductId, intent.CustomerBinding, matching[0], intentKey), linked.Token).ConfigureAwait(false);
                if (result.Proof != null)
                {
                    var matches = result.Proof.PackageName == product.PackageName && result.Proof.StoreProductId == product.StoreProductId;
                    var record = await store.ProtectAndCommitAsync(owner, result.Proof, ids.NewId(), matches ? intentKey : (Guid?)null, CancellationToken.None).ConfigureAwait(false);
                    Ensure(requested);
                    await SubmitAsync(record, requested, linked.Token).ConfigureAwait(false);
                }
                Ensure(requested);
                linked.Token.ThrowIfCancellationRequested();
                return new NativeStartOutcome(intentKey, result.Proof != null &&
                    (result.Proof.PackageName != product.PackageName || result.Proof.StoreProductId != product.StoreProductId)
                        ? NativeLaunchStatus.Unresolved : result.Status);
            }
            finally { serial.Release(); }
        }
        /// <summary>Resolves interrupted intent creation without reopening any native purchase sheet.</summary>
        public async Task ResumeAttemptsAsync(ScopedOwnerContext requested, CancellationToken token)
        {
            using var linked = Link(requested, token);
            await serial.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                var attempts = await store.ReadAttemptsAsync(owner, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                foreach (var attempt in attempts)
                {
                    var intent = attempt.Intent == null
                        ? await remote.CreateIntentAsync(attempt.IntentKey, attempt.Product.ProductId, attempt.Product.Version, linked.Token).ConfigureAwait(false)
                        : await remote.StatusAsync(attempt.Intent.IntentId, linked.Token).ConfigureAwait(false);
                    Ensure(requested);
                    if (intent.IntentKey != attempt.IntentKey || intent.ProductId != attempt.Product.ProductId ||
                        intent.ProductVersion != attempt.Product.Version || intent.PackageName != attempt.Product.PackageName ||
                        intent.StoreProductId != attempt.Product.StoreProductId || intent.Environment != attempt.Product.Environment ||
                        intent.Source != "purchase" || (attempt.Intent != null &&
                            (intent.IntentId != attempt.Intent.IntentId || intent.CustomerBinding != attempt.Intent.CustomerBinding)))
                        throw new InvalidOperationException("Retained native intent identity changed.");
                    await store.CommitIntentAsync(owner, attempt.IntentKey, intent, linked.Token).ConfigureAwait(false);
                }
            }
            finally { serial.Release(); }
        }
        /// <summary>Explicitly launches a prepared attempt only if no launch was previously requested.</summary>
        public async Task<NativeStartOutcome> ContinuePreparedAsync(ScopedOwnerContext requested, Guid intentKey, NativeStoreProduct confirmedPrice, CancellationToken token)
        {
            if (confirmedPrice == null)
                throw new ArgumentNullException(nameof(confirmedPrice));
            using var linked = Link(requested, token);
            if (!await serial.WaitAsync(0, linked.Token).ConfigureAwait(false))
                throw new InvalidOperationException("A native purchase operation is already in progress.");
            try
            {
                var attempts = await store.ReadAttemptsAsync(owner, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                var attempt = attempts.SingleOrDefault(value => value.IntentKey == intentKey);
                if (attempt?.Intent == null || attempt.LaunchRequested)
                    throw new InvalidOperationException("This attempt cannot be launched again.");
                var intent = await remote.StatusAsync(attempt.Intent.IntentId, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                if (intent.IntentKey != intentKey || intent.IntentId != attempt.Intent.IntentId || !intent.LaunchAllowed ||
                    intent.State != "awaiting_proof" || intent.Source != "purchase" ||
                    intent.PackageName != attempt.Product.PackageName || intent.StoreProductId != attempt.Product.StoreProductId ||
                    intent.ProductId != attempt.Product.ProductId || intent.ProductVersion != attempt.Product.Version ||
                    intent.Environment != attempt.Product.Environment || intent.CustomerBinding != attempt.Intent.CustomerBinding)
                    throw new InvalidOperationException("The prepared native intent cannot be launched.");
                await bridge.ConnectAsync(linked.Token).ConfigureAwait(false);
                Ensure(requested);
                var details = await bridge.QueryProductsAsync(new[] { attempt.Product }, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                var matching = details.Where(p => p.PackageName == intent.PackageName && p.StoreProductId == intent.StoreProductId &&
                    !string.IsNullOrWhiteSpace(p.DisplayPrice)).ToArray();
                if (matching.Length != 1 || confirmedPrice.PackageName != intent.PackageName ||
                    confirmedPrice.StoreProductId != intent.StoreProductId || confirmedPrice.DisplayPrice != matching[0].DisplayPrice)
                    throw new InvalidOperationException("Fresh native product details are unavailable.");
                await store.MarkLaunchRequestedAsync(owner, intentKey, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                var result = await bridge.LaunchAsync(new NativeLaunchRequest(intent.PackageName, intent.StoreProductId, intent.CustomerBinding, matching[0], intentKey), linked.Token).ConfigureAwait(false);
                if (result.Proof != null)
                {
                    var matches = result.Proof.PackageName == intent.PackageName && result.Proof.StoreProductId == intent.StoreProductId;
                    var record = await store.ProtectAndCommitAsync(owner, result.Proof, ids.NewId(), matches ? intentKey : (Guid?)null, CancellationToken.None).ConfigureAwait(false);
                    Ensure(requested);
                    await SubmitAsync(record, requested, linked.Token).ConfigureAwait(false);
                    if (!matches)
                        return new NativeStartOutcome(intentKey, NativeLaunchStatus.Unresolved);
                }
                Ensure(requested);
                return new NativeStartOutcome(intentKey, result.Status);
            }
            finally { serial.Release(); }
        }
        /// <summary>Protects unfinished proofs first, then retries each retained recovery identity.</summary>
        public async Task RecoverAsync(ScopedOwnerContext requested, CancellationToken token)
        {
            using var linked = Link(requested, token);
            await serial.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                int unresolved = 0;
                IReadOnlyList<NativeProof> unfinished;
                try
                {
                    await bridge.ConnectAsync(linked.Token).ConfigureAwait(false);
                    Ensure(requested);
                    var scan = await bridge.QueryUnfinishedAsync(linked.Token).ConfigureAwait(false);
                    unfinished = scan.Proofs;
                    unresolved = scan.UnresolvedCount;
                    Ensure(requested);
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    Ensure(requested);
                    unresolved++;
                    unfinished = Array.Empty<NativeProof>();
                }
                foreach (var proof in unfinished)
                {
                    try
                    {
                        await store.ProtectAndCommitAsync(owner, proof, ids.NewId(), null, linked.Token).ConfigureAwait(false);
                        Ensure(requested);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { Ensure(requested); unresolved++; }
                }
                var records = await store.ReadProtectedRecordsAsync(owner, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                foreach (var record in records)
                {
                    try
                    { await SubmitAsync(record, requested, linked.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { throw; }
                    catch { Ensure(requested); unresolved++; }
                }
                if (unresolved != 0)
                    throw new NativeRecoveryException(unresolved);
            }
            finally { serial.Release(); }
        }
        private async Task SubmitAsync(ProtectedNativeProofRecord record, ScopedOwnerContext requested, CancellationToken token)
        {
            var proof = await store.OpenForSubmissionAsync(owner, record.ProofId, token).ConfigureAwait(false);
            Ensure(requested);
            if (proof.PackageName != record.PackageName || proof.StoreProductId != record.StoreProductId)
                throw new InvalidOperationException("Protected proof identity changed.");
            NativeAttempt? attempt = null;
            if (record.ReservedAttemptKey.HasValue)
            {
                var attempts = await store.ReadAttemptsAsync(owner, token).ConfigureAwait(false);
                Ensure(requested);
                attempt = attempts.SingleOrDefault(value => value.IntentKey == record.ReservedAttemptKey.Value);
                if (attempt?.Intent == null || !attempt.LaunchRequested || attempt.Product.PackageName != proof.PackageName ||
                    attempt.Product.StoreProductId != proof.StoreProductId)
                    throw new InvalidOperationException("Protected callback reservation is invalid.");
            }
            NativeIntentState state;
            try
            {
                state = attempt == null
                    ? await remote.RecoverProofAsync(record.RecoveryIntentKey, proof, token).ConfigureAwait(false)
                    : await remote.SubmitProofAsync(attempt.Intent!.IntentId, proof, token).ConfigureAwait(false);
            }
            catch (NativePurchaseRemoteException error) when (attempt != null && error.StatusCode == 409)
            {
                Ensure(requested);
                attempt = null;
                state = await remote.RecoverProofAsync(record.RecoveryIntentKey, proof, token).ConfigureAwait(false);
            }
            Ensure(requested);
            if (state.IntentKey != (attempt?.IntentKey ?? record.RecoveryIntentKey) ||
                state.Source != (attempt == null ? "recovery" : "purchase") || state.LaunchAllowed ||
                state.PackageName != record.PackageName || state.StoreProductId != record.StoreProductId ||
                (attempt != null && state.IntentId != attempt.Intent!.IntentId))
                throw new InvalidOperationException("Recovery response identity changed.");
            if (attempt != null)
            {
                await store.CommitIntentAsync(owner, attempt.IntentKey, state, token).ConfigureAwait(false);
                Ensure(requested);
            }
            if (state.State == "granted" && state.FinishState == "finished" && state.ChargeId.HasValue)
                await store.MarkFinalizedAsync(owner, record.ProofId, state.ChargeId.Value, token).ConfigureAwait(false);
        }
        public async Task<bool> IsDeliveredAsync(ScopedOwnerContext requested, Guid intentId, CancellationToken token)
        {
            using var linked = Link(requested, token);
            var state = await remote.StatusAsync(intentId, linked.Token).ConfigureAwait(false);
            Ensure(requested);
            if (state.IntentId != intentId)
                throw new InvalidOperationException("Native intent identity changed.");
            if (state.State != "granted" || !state.FeedRevision.HasValue || !state.VisibilityGeneration.HasValue || !state.LogEpoch.HasValue || !state.GrantId.HasValue)
                return false;
            var checkpoint = await checkpoints.ReadAsync(owner, linked.Token).ConfigureAwait(false);
            Ensure(requested);
            linked.Token.ThrowIfCancellationRequested();
            return checkpoint != null && checkpoint.LogEpoch == state.LogEpoch && checkpoint.VisibilityGeneration == state.VisibilityGeneration &&
                checkpoint.CommittedThrough >= state.FeedRevision;
        }
        public async Task<bool> TryClaimPresentationAsync(ScopedOwnerContext requested, Guid intentId, CancellationToken token)
        {
            using var linked = Link(requested, token);
            var state = await remote.StatusAsync(intentId, linked.Token).ConfigureAwait(false);
            Ensure(requested);
            if (state.IntentId != intentId || state.State != "granted" || !state.FeedRevision.HasValue ||
                !state.VisibilityGeneration.HasValue || !state.LogEpoch.HasValue || !state.GrantId.HasValue)
                return false;
            var checkpoint = await checkpoints.ReadAsync(owner, linked.Token).ConfigureAwait(false);
            Ensure(requested);
            if (checkpoint == null || checkpoint.LogEpoch != state.LogEpoch || checkpoint.VisibilityGeneration != state.VisibilityGeneration ||
                checkpoint.CommittedThrough < state.FeedRevision)
                return false;
            if (!await store.TryClaimPresentationAsync(owner, state.GrantId.Value, linked.Token).ConfigureAwait(false))
                return false;
            Ensure(requested);
            checkpoint = await checkpoints.ReadAsync(owner, linked.Token).ConfigureAwait(false);
            Ensure(requested);
            return checkpoint != null && checkpoint.LogEpoch == state.LogEpoch && checkpoint.VisibilityGeneration == state.VisibilityGeneration &&
                checkpoint.CommittedThrough >= state.FeedRevision;
        }
        private CancellationTokenSource Link(ScopedOwnerContext requested, CancellationToken token)
        { Ensure(requested); return CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); }
        private void Ensure(ScopedOwnerContext requested)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(NativePurchaseCoordinator));
            if (requested != owner)
                throw new ArgumentException("The native purchase owner changed.", nameof(requested));
        }
        public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); }
    }
}
