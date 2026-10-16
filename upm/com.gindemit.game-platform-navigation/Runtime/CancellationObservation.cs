using System;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Navigation.Kernel
{
    /// <summary>
    /// Observes readiness without a worker-scheduled WhenAny completion action.
    /// Callers still await the original source for its outcome, outside their state locks.
    /// </summary>
    internal static class CancellationObservation
    {
        internal static async Task WaitAsync(Task source, CancellationToken cancellationToken)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.IsCompleted)
            {
                // A caller may choose cancellation over an already completed fault.
                if (source.IsFaulted)
                    _ = source.Exception;
                return;
            }
            if (!cancellationToken.CanBeCanceled)
            {
                try
                { await source; }
                catch { /* The caller reads the original outcome after readiness. */ }
                return;
            }

            // This private signal may complete inline. Published result/admission
            // promises retain their asynchronous-continuation contract.
            var completion = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken)))
            {
                // Start even when registration observed pre-cancellation: a provider
                // that ignores cancellation must still have its eventual fault observed.
                _ = ForwardCompletionAsync(source, completion);
                await completion.Task;
            }
        }

        private static async Task ForwardCompletionAsync(Task source, TaskCompletionSource<bool> completion)
        {
            try
            { await source; }
            catch { /* Observe late source failure even if cancellation already won. */ }
            completion.TrySetResult(true);
        }
    }
}
