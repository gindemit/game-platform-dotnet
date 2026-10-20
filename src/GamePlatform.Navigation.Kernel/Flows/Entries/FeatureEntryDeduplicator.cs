using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Navigation.Kernel.Flows
{
    /// <summary>
    /// Coalesces concurrent requests by caller-supplied key. It owns no navigation history;
    /// keys must include the relevant scope and delegates query/admit through the owning kernel.
    /// Caller cancellation stops only that caller's wait. The supplied scope token owns shared admission.
    /// All awaits, including cancellation and late-fault observation, preserve the calling context.
    /// </summary>
    public sealed class FeatureEntryDeduplicator<THandle> where THandle : class
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, PendingAdmission> _pending = new Dictionary<string, PendingAdmission>(StringComparer.Ordinal);

        /// <summary>Returns a current entry, joins the same pending admission, or starts one.</summary>
        /// <remarks>
        /// A successful admission must publish its handle so <paramref name="findCurrent"/> returns it
        /// before <paramref name="admit"/> completes; this lets later requests reuse the committed entry.
        /// </remarks>
        public async Task<FeatureEntryResolution<THandle>> ResolveAsync(string key,
            Func<string, THandle?> findCurrent,
            Func<string, CancellationToken, Task<THandle>> admit,
            CancellationToken scopeLifetime,
            CancellationToken callerCancellation = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A deduplication key is required.", nameof(key));
            if (findCurrent == null)
                throw new ArgumentNullException(nameof(findCurrent));
            if (admit == null)
                throw new ArgumentNullException(nameof(admit));
            callerCancellation.ThrowIfCancellationRequested();
            scopeLifetime.ThrowIfCancellationRequested();

            PendingAdmission pending;
            bool ownsAdmission;
            lock (_gate)
            {
                if (_pending.TryGetValue(key, out PendingAdmission? existing) && !existing.ScopeLifetime.IsCancellationRequested)
                {
                    pending = existing;
                    ownsAdmission = false;
                }
                else
                {
                    pending = new PendingAdmission(scopeLifetime);
                    _pending[key] = pending;
                    ownsAdmission = true;
                }
            }

            // Delegate may synchronously throw or re-enter the owner. It must run outside _gate.
            if (ownsAdmission)
                _ = CompleteAdmissionAsync(key, pending, findCurrent, admit, scopeLifetime);
            THandle handle = await WaitForCallerAsync(pending.Completion.Task, callerCancellation);
            callerCancellation.ThrowIfCancellationRequested();
            scopeLifetime.ThrowIfCancellationRequested();
            return new FeatureEntryResolution<THandle>(ownsAdmission ? pending.Kind : FeatureEntryResolutionKind.JoinedPendingAdmission, handle);
        }

        private async Task CompleteAdmissionAsync(string key, PendingAdmission pending,
            Func<string, THandle?> findCurrent,
            Func<string, CancellationToken, Task<THandle>> admit, CancellationToken scopeLifetime)
        {
            Task<THandle>? admission = null;
            try
            {
                scopeLifetime.ThrowIfCancellationRequested();
                // Reserve before querying: an old no-current observation must never outlive its reservation.
                THandle? current = findCurrent(key);
                scopeLifetime.ThrowIfCancellationRequested();
                if (current != null)
                {
                    pending.Kind = FeatureEntryResolutionKind.ReusedCurrent;
                    RemovePending(key, pending);
                    pending.Completion.TrySetResult(current);
                    return;
                }
                // Invoke the owner outside _gate. A provider may throw synchronously or ignore
                // cancellation; neither case may leave all coalesced callers waiting on it.
                admission = admit(key, scopeLifetime);
                if (admission == null)
                    throw new InvalidOperationException("Entry admission returned no task.");

                await CancellationObservation.WaitAsync(admission, scopeLifetime);
                THandle handle = await admission;
                if (handle == null)
                    throw new InvalidOperationException("Entry admission returned no handle.");
                scopeLifetime.ThrowIfCancellationRequested();
                RemovePending(key, pending);
                pending.Completion.TrySetResult(handle);
            }
            catch (OperationCanceledException exception)
            {
                RemovePending(key, pending);
                pending.Completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                RemovePending(key, pending);
                pending.Completion.TrySetException(exception);
            }
            finally
            {
                RemovePending(key, pending);
            }
        }

        private void RemovePending(string key, PendingAdmission pending)
        {
            lock (_gate)
            {
                if (_pending.TryGetValue(key, out PendingAdmission? current) && ReferenceEquals(current, pending))
                    _pending.Remove(key);
            }
        }

        private static async Task<THandle> WaitForCallerAsync(Task<THandle> admission, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
                return await admission;
            await CancellationObservation.WaitAsync(admission, cancellationToken);
            return await admission;
        }

        private sealed class PendingAdmission
        {
            public PendingAdmission(CancellationToken scopeLifetime) { ScopeLifetime = scopeLifetime; }
            public CancellationToken ScopeLifetime { get; }
            public FeatureEntryResolutionKind Kind { get; set; } = FeatureEntryResolutionKind.Admitted;
            public TaskCompletionSource<THandle> Completion { get; } = new TaskCompletionSource<THandle>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
