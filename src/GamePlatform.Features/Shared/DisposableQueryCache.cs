#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Features.Contracts.Snapshots;

namespace GamePlatform.Features.Shared
{
    /// <summary>A bounded disposable optimization. It never owns durable feature state.</summary>
    public sealed class DisposableQueryCache<T> : IDisposable where T : class
    {
        private readonly object gate = new object();
        private readonly Dictionary<ScopedQueryKey, Entry> entries = new Dictionary<ScopedQueryKey, Entry>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly Func<long> nowMilliseconds;
        private readonly int capacity;
        private readonly long lifetimeMilliseconds;
        private bool disposed;

        public DisposableQueryCache(int capacity, TimeSpan lifetime, Func<long> nowMilliseconds)
        {
            if (capacity <= 0 || capacity > 4096) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(lifetime));
            this.nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
            this.capacity = capacity;
            lifetimeMilliseconds = checked((long)lifetime.TotalMilliseconds);
        }

        public Task<FeatureSnapshot<T>> GetOrRefreshAsync(ScopedQueryKey key,
            Func<CancellationToken, Task<FeatureSnapshot<T>>> refresh, CancellationToken cancellationToken)
        {
            if (refresh == null) throw new ArgumentNullException(nameof(refresh));
            cancellationToken.ThrowIfCancellationRequested();
            Task<FeatureSnapshot<T>> shared;
            lock (gate)
            {
                ThrowIfDisposed();
                var now = GetNow();
                RemoveExpired(now);
                if (entries.TryGetValue(key, out var existing)) shared = existing.Task;
                else
                {
                    EnsureCapacity();
                    var completion = new TaskCompletionSource<FeatureSnapshot<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var entry = new Entry(completion.Task, checked(now + lifetimeMilliseconds));
                    entries.Add(key, entry);
                    _ = RunRefreshAsync(key, entry, completion, refresh);
                    shared = completion.Task;
                }
            }
            return AwaitWaiterAsync(shared, cancellationToken);
        }

        public bool Invalidate(ScopedQueryKey key)
        {
            lock (gate) { ThrowIfDisposed(); return entries.Remove(key); }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                entries.Clear();
                lifetime.Cancel();
                lifetime.Dispose();
            }
        }

        private async Task RunRefreshAsync(ScopedQueryKey key, Entry entry,
            TaskCompletionSource<FeatureSnapshot<T>> completion,
            Func<CancellationToken, Task<FeatureSnapshot<T>>> refresh)
        {
            try
            {
                var result = await refresh(lifetime.Token).ConfigureAwait(false);
                if (result == null) throw new InvalidOperationException("A refresh returned no snapshot.");
                if (result.Owner != key.Owner) throw new InvalidOperationException("A refresh returned a foreign owner or generation.");
                lock (gate)
                {
                    if (disposed || !entries.TryGetValue(key, out var current) || !ReferenceEquals(current, entry))
                        throw new OperationCanceledException(lifetime.Token);
                    current.LastPublished = result;
                    current.LastPublishedRevision = result.Revision;
                }
                completion.TrySetResult(result);
            }
            catch (Exception error)
            {
                lock (gate)
                {
                    if (!disposed && entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
                        entries.Remove(key);
                }
                if (error is OperationCanceledException cancelled) completion.TrySetCanceled(cancelled.CancellationToken);
                else completion.TrySetException(error);
            }
        }

        private static async Task<FeatureSnapshot<T>> AwaitWaiterAsync(Task<FeatureSnapshot<T>> shared, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled) return await shared.ConfigureAwait(false);
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(shared, cancelled.Task).ConfigureAwait(false) != shared)
                    throw new OperationCanceledException(cancellationToken);
            }
            return await shared.ConfigureAwait(false);
        }

        private long GetNow()
        {
            var now = nowMilliseconds();
            if (now < 0 || now > 253_402_300_799_999L) throw new InvalidOperationException("The cache clock returned an invalid timestamp.");
            return now;
        }

        private void RemoveExpired(long now)
        {
            var expired = new List<ScopedQueryKey>();
            foreach (var pair in entries) if (pair.Value.ExpiresAt <= now && pair.Value.Task.IsCompleted) expired.Add(pair.Key);
            foreach (var key in expired) entries.Remove(key);
        }

        private void EnsureCapacity()
        {
            if (entries.Count < capacity) return;
            ScopedQueryKey? oldestKey = null; long oldest = long.MaxValue;
            foreach (var pair in entries)
                if (pair.Value.Task.IsCompleted && pair.Value.ExpiresAt < oldest) { oldest = pair.Value.ExpiresAt; oldestKey = pair.Key; }
            if (!oldestKey.HasValue) throw new InvalidOperationException("The query cache is at capacity with refreshes in flight.");
            entries.Remove(oldestKey.Value);
        }

        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(DisposableQueryCache<T>)); }

        private sealed class Entry
        {
            public Entry(Task<FeatureSnapshot<T>> task, long expiresAt) { Task = task; ExpiresAt = expiresAt; }
            public Task<FeatureSnapshot<T>> Task { get; }
            public long ExpiresAt { get; }
            public long LastPublishedRevision { get; set; } = -1;
            public FeatureSnapshot<T>? LastPublished { get; set; }
        }
    }
}
