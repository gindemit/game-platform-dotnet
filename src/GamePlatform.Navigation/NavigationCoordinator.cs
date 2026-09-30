using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Navigation
{
    public enum RouteLoadResult { Available, Unavailable }
    public enum NavigationOutcome { Applied, Cancelled, Stale, Unavailable }
    public enum NavigationMode { Push, Replace }

    public interface IRouteLoader
    {
        Task<RouteLoadResult> LoadAsync(NavigationRoute route, CancellationToken cancellationToken);
    }

    public readonly struct ModalOwner : IEquatable<ModalOwner>
    {
        internal ModalOwner(long value) { Value = value; }
        internal long Value { get; }
        public bool Equals(ModalOwner other) => Value != 0 && Value == other.Value;
        public override bool Equals(object? obj) => obj is ModalOwner other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }

    public sealed class NavigationCoordinator : IDisposable
    {
        private readonly object gate = new object();
        private readonly List<NavigationRoute> history = new List<NavigationRoute>();
        private readonly Queue<ModalRoute> deferredModals = new Queue<ModalRoute>();
        private readonly int maximumHistory;
        private CancellationTokenSource? activeLoad;
        private long transition;
        private bool loading;
        private long modalSequence;
        private ModalOwner modalOwner;
        private ModalRoute? modal;
        private bool disposed;

        public NavigationCoordinator(NavigationRoute initialRoute, int maximumHistory = 16)
        {
            Root = initialRoute ?? throw new ArgumentNullException(nameof(initialRoute));
            Current = initialRoute;
            if (maximumHistory < 1 || maximumHistory > 64) throw new ArgumentOutOfRangeException(nameof(maximumHistory));
            this.maximumHistory = maximumHistory;
        }

        public NavigationRoute Root { get; }
        public NavigationRoute Current { get; private set; }
        public ModalRoute? Modal { get { lock (gate) return modal; } }
        public int HistoryCount { get { lock (gate) return history.Count; } }

        public async Task<NavigationOutcome> NavigateAsync(NavigationRoute route, NavigationMode mode, IRouteLoader loader, CancellationToken cancellationToken)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            CancellationTokenSource linked;
            long version;
            lock (gate)
            {
                EnsureActive();
                activeLoad?.Cancel();
                activeLoad?.Dispose();
                activeLoad = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linked = activeLoad;
                version = ++transition;
                loading = true;
            }
            RouteLoadResult loaded;
            try { loaded = await loader.LoadAsync(route, linked.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                lock (gate)
                {
                    if (version != transition) return NavigationOutcome.Stale;
                    loading = false;
                    return cancellationToken.IsCancellationRequested ? NavigationOutcome.Cancelled : NavigationOutcome.Stale;
                }
            }
            lock (gate)
            {
                if (disposed || version != transition || linked.IsCancellationRequested) return NavigationOutcome.Stale;
                loading = false;
                if (loaded == RouteLoadResult.Unavailable) return NavigationOutcome.Unavailable;
                if (mode == NavigationMode.Push)
                {
                    if (history.Count == maximumHistory) history.RemoveAt(0);
                    history.Add(Current);
                }
                Current = route;
                return NavigationOutcome.Applied;
            }
        }

        public ModalOwner OpenModal(ModalRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            lock (gate)
            {
                EnsureActive();
                modal = route;
                modalOwner = new ModalOwner(++modalSequence);
                return modalOwner;
            }
        }

        public bool CloseModal(ModalOwner owner)
        {
            lock (gate)
            {
                if (modal == null || !modalOwner.Equals(owner)) return false;
                modal = null;
                modalOwner = default;
                return true;
            }
        }

        public bool DeferModal(ModalRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            lock (gate)
            {
                EnsureActive();
                if (deferredModals.Count == 8) return false;
                deferredModals.Enqueue(route);
                return true;
            }
        }

        public bool TryOpenDeferredModal(out ModalOwner owner)
        {
            lock (gate)
            {
                EnsureActive();
                if (modal != null || deferredModals.Count == 0) { owner = default; return false; }
                modal = deferredModals.Dequeue();
                modalOwner = new ModalOwner(++modalSequence);
                owner = modalOwner;
                return true;
            }
        }

        public bool Back()
        {
            lock (gate)
            {
                EnsureActive();
                if (modal != null) { modal = null; modalOwner = default; return true; }
                if (loading) { CancelPendingLoad(); return true; }
                if (history.Count == 0) return false;
                Current = history[history.Count - 1];
                history.RemoveAt(history.Count - 1);
                return true;
            }
        }

        public void ReturnHome()
        {
            lock (gate)
            {
                EnsureActive();
                CancelPendingLoad();
                history.Clear();
                deferredModals.Clear();
                modal = null;
                modalOwner = default;
                Current = Root;
            }
        }

        private void CancelPendingLoad()
        {
            activeLoad?.Cancel();
            transition++;
            loading = false;
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                CancelPendingLoad();
                activeLoad?.Dispose();
                activeLoad = null;
                history.Clear();
                deferredModals.Clear();
                modal = null;
            }
        }

        private void EnsureActive()
        {
            if (disposed) throw new ObjectDisposedException(nameof(NavigationCoordinator));
        }
    }
}
