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

            THandle? current = findCurrent(key);
            if (current != null)
                return new FeatureEntryResolution<THandle>(FeatureEntryResolutionKind.ReusedCurrent, current);

            PendingAdmission pending;
            bool ownsAdmission;
            lock (_gate)
            {
                if (_pending.TryGetValue(key, out PendingAdmission? existing))
                {
                    pending = existing;
                    ownsAdmission = false;
                }
                else
                {
                    pending = new PendingAdmission();
                    _pending.Add(key, pending);
                    ownsAdmission = true;
                }
            }

            // Delegate may synchronously throw or re-enter the owner. It must run outside _gate.
            if (ownsAdmission)
                _ = CompleteAdmissionAsync(key, pending, admit, scopeLifetime);
            THandle handle = await WaitForCallerAsync(pending.Completion.Task, callerCancellation).ConfigureAwait(false);
            return new FeatureEntryResolution<THandle>(ownsAdmission ? FeatureEntryResolutionKind.Admitted : FeatureEntryResolutionKind.JoinedPendingAdmission, handle);
        }

        private async Task CompleteAdmissionAsync(string key, PendingAdmission pending,
            Func<string, CancellationToken, Task<THandle>> admit, CancellationToken scopeLifetime)
        {
            Task<THandle>? admission = null;
            try
            {
                scopeLifetime.ThrowIfCancellationRequested();
                // Invoke the owner outside _gate. A provider may throw synchronously or ignore
                // cancellation; neither case may leave all coalesced callers waiting on it.
                admission = admit(key, scopeLifetime);
                if (admission == null)
                    throw new InvalidOperationException("Entry admission returned no task.");

                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (scopeLifetime.Register(() => cancelled.TrySetResult(true)))
                {
                    Task winner = await Task.WhenAny(admission, cancelled.Task).ConfigureAwait(false);
                    if (!ReferenceEquals(winner, admission))
                    {
                        RemovePending(key, pending);
                        pending.Completion.TrySetCanceled(scopeLifetime);
                        ObserveLateAdmission(admission);
                        return;
                    }
                }

                THandle handle = await admission.ConfigureAwait(false);
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

        private static async void ObserveLateAdmission(Task admission)
        {
            try
            {
                await admission.ConfigureAwait(false);
            }
            catch
            {
                // Cancellation already settled callers; observe late provider failures.
            }
        }

        private static async Task<THandle> WaitForCallerAsync(Task<THandle> admission, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
                return await admission.ConfigureAwait(false);
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                Task winner = await Task.WhenAny(admission, cancelled.Task).ConfigureAwait(false);
                if (!ReferenceEquals(winner, admission))
                    throw new OperationCanceledException(cancellationToken);
                return await admission.ConfigureAwait(false);
            }
        }

        private sealed class PendingAdmission
        {
            public TaskCompletionSource<THandle> Completion { get; } = new TaskCompletionSource<THandle>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
