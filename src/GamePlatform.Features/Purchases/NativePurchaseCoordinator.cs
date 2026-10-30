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
                if (results.Count > 200 || (results.Count == 200 && page.NextIntentId.HasValue))
                    throw new InvalidOperationException("Native history requires explicit paging.");
                if (page.NextIntentId.HasValue && (page.Intents.Count == 0 || page.NextIntentId == after || !seen.Contains(page.NextIntentId.Value)))
                    throw new InvalidOperationException("Native recovery cursor did not advance.");
                after = page.NextIntentId;
            } while (after.HasValue);
            return results.AsReadOnly();
        }
        public async Task<NativeRecoveryPage> ReadPurchasePageAsync(ScopedOwnerContext requested, Guid? after, int limit, CancellationToken token)
        {
            if (limit < 1 || limit > 50)
                throw new ArgumentOutOfRangeException(nameof(limit));
            using var linked = Link(requested, token);
            var page = await remote.RecoveryAsync(after, limit, linked.Token).ConfigureAwait(false);
            Ensure(requested);
            if (page == null || page.Intents == null || page.Intents.Count > limit || page.Intents.Any(item => item == null) ||
                (page.NextIntentId.HasValue && (page.Intents.Count == 0 || page.NextIntentId != page.Intents[page.Intents.Count - 1].IntentId)))
                throw new InvalidOperationException("Invalid native history page.");
            string previous = after?.ToString("D") ?? "";
            foreach (var item in page.Intents)
            {
                string current = item.IntentId.ToString("D");
                if (string.CompareOrdinal(previous, current) >= 0)
                    throw new InvalidOperationException("Native history did not advance.");
                previous = current;
            }
            return page;
        }
        public async Task VisitAttemptsAsync(ScopedOwnerContext requested, Func<NativeAttempt, CancellationToken, Task> visitor, CancellationToken token)
        {
            if (visitor == null)
                throw new ArgumentNullException(nameof(visitor));
            using var linked = Link(requested, token);
            if (store is IPagedProtectedProofStore paged)
                await VisitPagesAsync(requested, (cursor, ct) => paged.ReadAttemptPageAsync(owner, cursor, 32, ct), visitor, linked.Token).ConfigureAwait(false);
            else
            {
                int maximum = RequireLegacyBound();
                var items = await store.ReadAttemptsAsync(owner, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                if (items.Count > maximum)
                    throw new InvalidOperationException("The legacy proof store exceeded its fixed bound.");
                foreach (var item in items)
                { Ensure(requested); linked.Token.ThrowIfCancellationRequested(); await visitor(item, linked.Token).ConfigureAwait(false); }
            }
        }
        public async Task<NativeAttempt?> FindAttemptAsync(ScopedOwnerContext requested, Guid intentKey, CancellationToken token)
        {
            using var linked = Link(requested, token);
            if (store is IPagedProtectedProofStore paged)
            {
                var item = await paged.FindAttemptAsync(owner, intentKey, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                if (item != null && item.IntentKey != intentKey)
                    throw new InvalidOperationException("Protected attempt identity changed.");
                return item;
            }
            NativeAttempt? result = null;
            await VisitAttemptsAsync(requested, (item, ct) =>
            {
                if (item.IntentKey == intentKey)
                { if (result != null) throw new InvalidOperationException("Duplicate protected attempt."); result = item; }
                return Task.CompletedTask;
            }, linked.Token).ConfigureAwait(false);
            return result;
        }
        public async Task<bool> HasUnresolvedAttemptAsync(ScopedOwnerContext requested, NativeProduct product, Guid? exceptIntentKey, CancellationToken token)
        {
            bool unresolved = false;
            await VisitAttemptsAsync(requested, (item, ct) =>
            {
                if (item.IntentKey != exceptIntentKey && item.Product.PackageName == product.PackageName && item.Product.StoreProductId == product.StoreProductId &&
                    (item.Intent == null || (item.Intent.State != "granted" && item.Intent.State != "rejected")))
                    unresolved = true;
                return Task.CompletedTask;
            }, token).ConfigureAwait(false);
            return unresolved;
        }
        private async Task VisitPagesAsync<T>(ScopedOwnerContext requested, Func<string?, CancellationToken, Task<NativeProtectedPage<T>>> read,
            Func<T, CancellationToken, Task> visitor, CancellationToken token)
        {
            string? cursor = null;
            long? highWater = null;
            long after = 0;
            do
            {
                token.ThrowIfCancellationRequested();
                var page = await read(cursor, token).ConfigureAwait(false);
                Ensure(requested);
                if (page == null || page.AfterSequence != after || (highWater.HasValue && page.HighWatermark != highWater.Value) ||
                    (page.NextCursor != null && page.NextCursor == cursor))
                    throw new InvalidOperationException("Protected-store page did not advance within its captured snapshot.");
                highWater = page.HighWatermark;
                foreach (var item in page.Items)
                { Ensure(requested); token.ThrowIfCancellationRequested(); await visitor(item, token).ConfigureAwait(false); }
                after = page.LastSequence;
                cursor = page.NextCursor;
            } while (cursor != null);
        }
        public async Task<NativeStartOutcome> StartAsync(ScopedOwnerContext requested, NativeProduct product, NativeStoreProduct confirmedPrice, CancellationToken token,
            bool acknowledgeUnresolvedPriorPurchase = false, Action<Guid>? onAttemptAllocated = null)
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
                if (!acknowledgeUnresolvedPriorPurchase && await HasUnresolvedAttemptAsync(requested, product, null, linked.Token).ConfigureAwait(false))
                    throw new InvalidOperationException("A prior native purchase remains unresolved.");
                var intentKey = ids.NewId();
                onAttemptAllocated?.Invoke(intentKey);
                await store.ReserveAttemptAsync(owner, intentKey, product, 8192, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                var intent = await remote.CreateIntentAsync(intentKey, product.ProductId, product.Version, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                if (intent.IntentKey != intentKey || intent.ProductId != product.ProductId || intent.ProductVersion != product.Version ||
                    intent.PackageName != product.PackageName || intent.StoreProductId != product.StoreProductId ||
                    intent.Environment != product.Environment || intent.ProductType != product.ProductType || intent.Source != "purchase" || !intent.LaunchAllowed || intent.State != "awaiting_proof")
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
                bool attributed = result.Proof == null;
                if (result.Proof != null)
                {
                    var record = await store.ProtectAndCommitAsync(owner, result.Proof, ids.NewId(), null, CancellationToken.None).ConfigureAwait(false);
                    attributed = record.ReservedAttemptKey == intentKey;
                    Ensure(requested);
                    await SubmitAsync(record, requested, linked.Token).ConfigureAwait(false);
                }
                Ensure(requested);
                linked.Token.ThrowIfCancellationRequested();
                return new NativeStartOutcome(intentKey, !attributed || result.Proof != null &&
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
                await VisitAttemptsAsync(requested, async (attempt, ct) =>
                {
                    var intent = attempt.Intent == null
                        ? await remote.CreateIntentAsync(attempt.IntentKey, attempt.Product.ProductId, attempt.Product.Version, linked.Token).ConfigureAwait(false)
                        : await remote.StatusAsync(attempt.Intent.IntentId, linked.Token).ConfigureAwait(false);
                    Ensure(requested);
                    if (intent.IntentKey != attempt.IntentKey || intent.ProductId != attempt.Product.ProductId ||
                        intent.ProductVersion != attempt.Product.Version || intent.PackageName != attempt.Product.PackageName ||
                        intent.StoreProductId != attempt.Product.StoreProductId || intent.Environment != attempt.Product.Environment || intent.ProductType != attempt.Product.ProductType ||
                        intent.Source != "purchase" || (attempt.Intent != null &&
                            (intent.IntentId != attempt.Intent.IntentId || intent.CustomerBinding != attempt.Intent.CustomerBinding)))
                        throw new InvalidOperationException("Retained native intent identity changed.");
                    await store.CommitIntentAsync(owner, attempt.IntentKey, intent, linked.Token).ConfigureAwait(false);
                }, linked.Token).ConfigureAwait(false);
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
                var attempt = await FindAttemptAsync(requested, intentKey, linked.Token).ConfigureAwait(false);
                if (attempt?.Intent == null || attempt.LaunchRequested)
                    throw new InvalidOperationException("This attempt cannot be launched again.");
                var intent = await remote.StatusAsync(attempt.Intent.IntentId, linked.Token).ConfigureAwait(false);
                Ensure(requested);
                if (intent.IntentKey != intentKey || intent.IntentId != attempt.Intent.IntentId || !intent.LaunchAllowed ||
                    intent.State != "awaiting_proof" || intent.Source != "purchase" ||
                    intent.PackageName != attempt.Product.PackageName || intent.StoreProductId != attempt.Product.StoreProductId ||
                    intent.ProductId != attempt.Product.ProductId || intent.ProductVersion != attempt.Product.Version ||
                    intent.Environment != attempt.Product.Environment || intent.ProductType != attempt.Product.ProductType || intent.CustomerBinding != attempt.Intent.CustomerBinding)
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
                    var record = await store.ProtectAndCommitAsync(owner, result.Proof, ids.NewId(), null, CancellationToken.None).ConfigureAwait(false);
                    Ensure(requested);
                    await SubmitAsync(record, requested, linked.Token).ConfigureAwait(false);
                    if (!matches || record.ReservedAttemptKey != intentKey)
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
                async Task SubmitRecord(ProtectedNativeProofRecord record, CancellationToken ct)
                {
                    try
                    { await SubmitAsync(record, requested, linked.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { throw; }
                    catch { Ensure(requested); unresolved++; }
                }
                if (store is IPagedProtectedProofStore paged)
                    await VisitPagesAsync(requested, (cursor, ct) => paged.ReadProofPageAsync(owner, cursor, 32, ct), SubmitRecord, linked.Token).ConfigureAwait(false);
                else
                {
                    int maximum = RequireLegacyBound();
                    var records = await store.ReadProtectedRecordsAsync(owner, linked.Token).ConfigureAwait(false);
                    Ensure(requested);
                    if (records.Count > maximum)
                        throw new InvalidOperationException("The legacy proof store exceeded its fixed bound.");
                    foreach (var record in records)
                        await SubmitRecord(record, linked.Token).ConfigureAwait(false);
                }
                if (unresolved != 0)
                    throw new NativeRecoveryException(unresolved);
            }
            finally { serial.Release(); }
        }
        private async Task SubmitAsync(ProtectedNativeProofRecord record, ScopedOwnerContext requested, CancellationToken token)
        {
            Ensure(requested);
            token.ThrowIfCancellationRequested();
            if (record.FinalizedChargeId.HasValue)
                return;
            var proof = await store.OpenForSubmissionAsync(owner, record.ProofId, token).ConfigureAwait(false);
            Ensure(requested);
            if (proof.PackageName != record.PackageName || proof.StoreProductId != record.StoreProductId)
                throw new InvalidOperationException("Protected proof identity changed.");
            NativeAttempt? attempt = null;
            if (record.ReservedAttemptKey.HasValue)
            {
                attempt = await FindAttemptAsync(requested, record.ReservedAttemptKey.Value, token).ConfigureAwait(false);
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
        private int RequireLegacyBound()
        {
            if (!(store is IBoundedProtectedProofStore bounded) || bounded.MaximumRecords < 1 || bounded.MaximumRecords > 256)
                throw new InvalidOperationException("The proof store requires paged reads or an enforced fixed legacy bound.");
            return bounded.MaximumRecords;
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
