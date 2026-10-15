using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Navigation;

namespace GamePlatform.Tests.Navigation
{
    public sealed class NavigationCompatibilityTests
    {
        [Fact]
        public async Task ModalSurvivesPushReplaceAndOldestHistoryStillEvicts()
        {
            var root = new HomeRoute();
            using var navigation = new NavigationCoordinator(root, maximumHistory: 1);
            var owner = navigation.OpenModal(new ModalRoute("persistent"));
            var first = new DetailRoute("one", new CallerReturnContext(root, "one"));
            await navigation.NavigateAsync(first, NavigationMode.Push, new Loader(), default);
            Assert.Equal("persistent", navigation.Modal!.ModalKey);
            var replacement = new DetailRoute("replace", new CallerReturnContext(root, "replace"));
            await navigation.NavigateAsync(replacement, NavigationMode.Replace, new Loader(), default);
            Assert.True(navigation.CloseModal(owner));
            var last = new DetailRoute("last", new CallerReturnContext(root, "last"));
            await navigation.NavigateAsync(last, NavigationMode.Push, new Loader(), default);
            Assert.Equal(1, navigation.HistoryCount);
            Assert.True(navigation.Back());
            Assert.Same(replacement, navigation.Current);
            Assert.False(navigation.Back());
            navigation.ReturnHome();
            Assert.Same(root, navigation.Current);
        }

        [Fact]
        public async Task OpeningModalDoesNotCancelPendingRouteAndBackConsumesModalOnly()
        {
            using var navigation = new NavigationCoordinator(new HomeRoute());
            var completion = new TaskCompletionSource<RouteLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = navigation.NavigateAsync(new HomeRoute(), NavigationMode.Push,
                new Loader((_, __) => completion.Task), default);
            navigation.OpenModal(new ModalRoute("pause"));
            Assert.True(navigation.Back());
            Assert.False(pending.IsCompleted);
            completion.SetResult(RouteLoadResult.Available);
            Assert.Equal(NavigationOutcome.Applied, await pending);
        }

        [Fact]
        public async Task LoaderFaultStillPropagatesAndPrecancelledLoaderStillReportsCancelled()
        {
            using var navigation = new NavigationCoordinator(new HomeRoute());
            await Assert.ThrowsAsync<InvalidOperationException>(() => navigation.NavigateAsync(new HomeRoute(), NavigationMode.Push,
                new Loader((_, __) => throw new InvalidOperationException("loader")), default));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Equal(NavigationOutcome.Cancelled, await navigation.NavigateAsync(new HomeRoute(), NavigationMode.Push, new Loader(), cancellation.Token));
        }

        [Fact]
        public async Task IgnoredCancellationReturnsStaleAndDisposePreservesLastCurrentValue()
        {
            var navigation = new NavigationCoordinator(new HomeRoute());
            using var cancellation = new CancellationTokenSource();
            var completion = new TaskCompletionSource<RouteLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = navigation.NavigateAsync(new HomeRoute(), NavigationMode.Push,
                new Loader((_, __) => completion.Task), cancellation.Token);
            cancellation.Cancel();
            Assert.False(pending.IsCompleted);
            completion.SetResult(RouteLoadResult.Available);
            Assert.Equal(NavigationOutcome.Stale, await pending);
            var last = new HomeRoute();
            await navigation.NavigateAsync(last, NavigationMode.Push, new Loader(), default);
            navigation.Dispose();
            Assert.Same(last, navigation.Current);
            Assert.Equal(0, navigation.HistoryCount);
            Assert.Null(navigation.Modal);
            Assert.Throws<ObjectDisposedException>(() => navigation.Back());
        }

        private sealed class Loader : IRouteLoader
        {
            private readonly Func<NavigationRoute, CancellationToken, Task<RouteLoadResult>> load;
            public Loader(Func<NavigationRoute, CancellationToken, Task<RouteLoadResult>>? load = null)
            {
                this.load = load ?? ((_, token) => { token.ThrowIfCancellationRequested(); return Task.FromResult(RouteLoadResult.Available); });
            }
            public Task<RouteLoadResult> LoadAsync(NavigationRoute route, CancellationToken cancellationToken) => load(route, cancellationToken);
        }
    }
}
