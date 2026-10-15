using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Navigation.Kernel;
using KernelOutcome = GamePlatform.Navigation.Kernel.NavigationOutcome;

namespace GamePlatform.Navigation
{
    /// <summary>Compatibility facade. All committed history and modal ownership live in the portable kernel.</summary>
    public sealed class NavigationCoordinator : IDisposable
    {
        private static readonly NavigationDefinition<NavigationRoute, bool> Screen = new NavigationDefinition<NavigationRoute, bool>("legacy.route");
        private static readonly NavigationDefinition<ModalRoute, bool> Dialog = new NavigationDefinition<ModalRoute, bool>("legacy.modal", NavigationPresentation.Modal);
        private readonly object gate = new object();
        private readonly NavigationKernel kernel;
        private readonly Queue<ModalRoute> deferredModals = new Queue<ModalRoute>();
        private bool disposed;
        private NavigationRoute? disposedCurrent;

        public NavigationCoordinator(NavigationRoute initialRoute, int maximumHistory = 16)
        {
            Root = initialRoute ?? throw new ArgumentNullException(nameof(initialRoute));
            if (maximumHistory < 1 || maximumHistory > 64)
                throw new ArgumentOutOfRangeException(nameof(maximumHistory));
            kernel = new NavigationKernel("legacy", 0, maximumHistory, legacy: true);
            Immediate(kernel.SelectRootAsync(new NavigationRequest<NavigationRoute, bool>(Screen, Root)));
        }

        public NavigationRoute Root { get; }
        public NavigationRoute Current => kernel.Snapshot.Current?.Entry.GetArguments<NavigationRoute>() ?? disposedCurrent ?? Root;
        public ModalRoute? Modal
        {
            get { var snapshot = kernel.Snapshot; return snapshot.Modals.Count == 0 ? null : snapshot.Top!.Entry.GetArguments<ModalRoute>(); }
        }
        public int HistoryCount => Math.Max(0, kernel.Snapshot.Entries.Count - 1);

        public async Task<NavigationOutcome> NavigateAsync(NavigationRoute route, NavigationMode mode, IRouteLoader loader, CancellationToken cancellationToken)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));
            if (loader == null)
                throw new ArgumentNullException(nameof(loader));
            EnsureActive();
            ExceptionDispatchInfo? failure = null;
            var loaderCancelled = false;
            async Task<NavigationAdmissionOutcome> Load(NavigationEntry entry, CancellationToken token)
            {
                try
                {
                    return await loader.LoadAsync(route, token).ConfigureAwait(false) == RouteLoadResult.Available
                        ? NavigationAdmissionOutcome.Available : NavigationAdmissionOutcome.Unavailable;
                }
                catch (OperationCanceledException) { loaderCancelled = true; throw; }
                catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); throw; }
            }
            var request = new NavigationRequest<NavigationRoute, bool>(Screen, route);
            var result = mode == NavigationMode.Push
                ? await kernel.PushAsync(request, Load, cancellationToken).ConfigureAwait(false)
                : await kernel.ReplaceAsync(request, Load, cancellationToken).ConfigureAwait(false);
            failure?.Throw();
            return result.Outcome switch
            {
                KernelOutcome.Applied => NavigationOutcome.Applied,
                KernelOutcome.Unavailable => NavigationOutcome.Unavailable,
                KernelOutcome.Cancelled when loaderCancelled && cancellationToken.IsCancellationRequested => NavigationOutcome.Cancelled,
                _ => NavigationOutcome.Stale
            };
        }

        public ModalOwner OpenModal(ModalRoute route)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));
            EnsureActive();
            var owner = kernel.Snapshot.Current!.Entry.Id;
            var opened = Immediate(kernel.OpenModalAsync(owner, new NavigationRequest<ModalRoute, bool>(Dialog, route)));
            return new ModalOwner(opened.Entry!.Id.Value);
        }

        public bool CloseModal(ModalOwner owner)
        {
            var top = kernel.Snapshot.Top;
            return top != null && top.Entry.Presentation == NavigationPresentation.Modal && top.Entry.Id.Value == owner.Value &&
                kernel.Close(top.Entry.Id) == KernelOutcome.Applied;
        }

        public bool DeferModal(ModalRoute route)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));
            lock (gate)
            {
                EnsureActive();
                if (deferredModals.Count == 8)
                    return false;
                deferredModals.Enqueue(route);
                return true;
            }
        }

        public bool TryOpenDeferredModal(out ModalOwner owner)
        {
            ModalRoute route;
            lock (gate)
            {
                EnsureActive();
                if (kernel.Snapshot.Modals.Count != 0 || deferredModals.Count == 0)
                { owner = default; return false; }
                route = deferredModals.Dequeue();
            }
            owner = OpenModal(route);
            return true;
        }

        public bool Back() { EnsureActive(); return kernel.Back() == KernelOutcome.Applied; }

        public void ReturnHome()
        {
            lock (gate)
            { EnsureActive(); deferredModals.Clear(); }
            Immediate(kernel.SelectRootAsync(new NavigationRequest<NavigationRoute, bool>(Screen, Root)));
        }

        public void Dispose()
        {
            lock (gate)
            { if (disposed) return; disposedCurrent = Current; disposed = true; deferredModals.Clear(); }
            kernel.Dispose();
        }

        private void EnsureActive()
        {
            lock (gate)
                if (disposed)
                    throw new ObjectDisposedException(nameof(NavigationCoordinator));
        }

        private static T Immediate<T>(Task<T> completed)
        {
            // No admission delegate is supplied by synchronous compatibility operations, so these never wait.
            if (!completed.IsCompleted)
                throw new InvalidOperationException("Immediate kernel command unexpectedly yielded.");
            return completed.GetAwaiter().GetResult();
        }
    }
}
