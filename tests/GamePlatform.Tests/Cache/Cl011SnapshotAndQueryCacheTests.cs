using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Shared;

namespace GamePlatform.Tests.Cache
{
    public sealed class Cl011SnapshotAndQueryCacheTests
    {
        [Fact]
        public void SnapshotStatesRejectSuccessShapedMissingAndForeignDiagnostics()
        {
            var owner = Owner(1, "view-a", 4);
            Assert.Throws<ArgumentException>(() => new FeatureSnapshot<Model>(owner, 0, SnapshotFreshness.Missing, new Model("fake")));
            Assert.Throws<ArgumentException>(() => new FeatureSnapshot<Model>(owner, 1, FeatureSnapshotState.Error, SnapshotFreshness.Stale, new Model("old"), 5, null));
            var pending = new FeatureSnapshot<Model>(owner, 7, FeatureSnapshotState.Pending, SnapshotFreshness.Stale, new Model("old"), 10, null);
            Assert.Equal(7, pending.Revision);
            Assert.Equal(FeatureSnapshotState.Pending, pending.State);
        }

        [Fact]
        public async Task CacheSeparatesAccountsAppsViewsGenerationsAndQueries()
        {
            long now = 100;
            using var cache = new DisposableQueryCache<Model>(8, TimeSpan.FromMinutes(1), () => now);
            var keys = new[]
            {
                Key(Owner(1, "view", 1), "q:a"), Key(Owner(2, "view", 1), "q:a"),
                Key(Owner(1, "other", 1), "q:a"), Key(Owner(1, "view", 2), "q:a"),
                Key(Owner(1, "view", 1), "q:b")
            };
            var calls = 0;
            foreach (var key in keys)
            {
                var observed = await cache.GetOrRefreshAsync(key, _ =>
                {
                    calls++;
                    return Task.FromResult(Current(key.Owner, calls));
                }, CancellationToken.None);
                Assert.Equal(calls.ToString(), observed.Value!.Value);
            }
            Assert.Equal(keys.Length, calls);
        }

        [Fact]
        public async Task CancelledWaiterDoesNotCancelSharedRefresh()
        {
            using var cache = new DisposableQueryCache<Model>(2, TimeSpan.FromMinutes(1), () => 10);
            var key = Key(Owner(1, "view", 1), "query");
            var release = new TaskCompletionSource<FeatureSnapshot<Model>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            using var cancelled = new CancellationTokenSource();
            var first = cache.GetOrRefreshAsync(key, _ => { calls++; return release.Task; }, cancelled.Token);
            var second = cache.GetOrRefreshAsync(key, _ => { calls++; return release.Task; }, CancellationToken.None);
            cancelled.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => first);
            release.SetResult(Current(key.Owner, 9));
            Assert.Equal(9, (await second).Revision);
            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task InvalidatedLateRefreshCannotReplaceNewerEntry()
        {
            using var cache = new DisposableQueryCache<Model>(2, TimeSpan.FromMinutes(1), () => 10);
            var key = Key(Owner(1, "view", 1), "query");
            var old = new TaskCompletionSource<FeatureSnapshot<Model>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var oldWaiter = cache.GetOrRefreshAsync(key, _ => old.Task, CancellationToken.None);
            Assert.True(cache.Invalidate(key));
            var newer = await cache.GetOrRefreshAsync(key, _ => Task.FromResult(Current(key.Owner, 12)), CancellationToken.None);
            old.SetResult(Current(key.Owner, 11));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => oldWaiter);
            var observed = await cache.GetOrRefreshAsync(key, _ => throw new InvalidOperationException("must not refresh"), CancellationToken.None);
            Assert.Equal(newer.Revision, observed.Revision);
        }

        [Fact]
        public async Task DisposalCancelsOwnedRefreshButNotCallerResources()
        {
            var cache = new DisposableQueryCache<Model>(1, TimeSpan.FromMinutes(1), () => 10);
            var key = Key(Owner(1, "view", 1), "query");
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var task = cache.GetOrRefreshAsync(key, async token =>
            {
                started.SetResult(true);
                await Task.Delay(TimeSpan.FromMinutes(1), token);
                return Current(key.Owner, 1);
            }, CancellationToken.None);
            await started.Task;
            cache.Dispose();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.GetOrRefreshAsync(key, _ => Task.FromResult(Current(key.Owner, 1)), CancellationToken.None));
        }

        private static FeatureSnapshot<Model> Current(ScopedOwnerContext owner, long revision) =>
            new FeatureSnapshot<Model>(owner, revision, FeatureSnapshotState.Available, SnapshotFreshness.Current, new Model(revision.ToString()), 10, null);
        private static ScopedQueryKey Key(ScopedOwnerContext owner, string query) => new ScopedQueryKey(owner, "test.feature", query);
        private static ScopedOwnerContext Owner(int discriminator, string view, long generation) => new ScopedOwnerContext(
            new OwnerScope(new BackendNamespace("backend"), new AppId(Guid.Parse($"0199f9a0-000{discriminator}-7000-8000-000000000001")),
                new PlatformUserId(Guid.Parse($"0199f9a0-000{discriminator}-7000-8000-000000000002"))), new SemanticId(view), generation);
        private sealed class Model { public Model(string value) { Value = value; } public string Value { get; } }
    }
}
