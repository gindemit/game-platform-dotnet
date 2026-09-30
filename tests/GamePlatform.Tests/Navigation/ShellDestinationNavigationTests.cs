using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Navigation;

namespace GamePlatform.Tests.Navigation
{
    public sealed class ShellDestinationNavigationTests
    {
        [Fact]
        public async Task ConfiguredDestinationRootOwnsItsDetailStackAndReturn()
        {
            var packs = new DestinationRoute("packs");
            using var navigation = new NavigationCoordinator(packs);
            var detail = new DetailRoute("pack-detail:forest", new CallerReturnContext(packs, "packs-list"));
            Assert.Equal(NavigationOutcome.Applied, await navigation.NavigateAsync(detail, NavigationMode.Push, new ImmediateLoader(), CancellationToken.None));
            Assert.Same(packs, Assert.IsType<DetailRoute>(navigation.Current).ReturnContext.Route);
            Assert.True(navigation.Back());
            Assert.Same(packs, navigation.Current);
            Assert.False(navigation.Back());

            await navigation.NavigateAsync(detail, NavigationMode.Push, new ImmediateLoader(), CancellationToken.None);
            navigation.ReturnHome();
            Assert.Same(packs, navigation.Current);
            Assert.Same(packs, navigation.Root);
            Assert.Equal(0, navigation.HistoryCount);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void DestinationAndDetailKeysAreValidated(string key)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DestinationRoute(key));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DetailRoute(key, new CallerReturnContext(new HomeRoute(), "home")));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DestinationRoute(new string('k', 129)));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BackDuringDelayedLoadIsOneActionAndTheLateRouteNeverPublishes(bool loaderIgnoresCancellation)
        {
            var home = new HomeRoute();
            using var navigation = new NavigationCoordinator(home);
            var slow = new DeferredLoader(loaderIgnoresCancellation);
            var pending = navigation.NavigateAsync(new PlayRoute(new SemanticId("late"), 1), NavigationMode.Push, slow, CancellationToken.None);
            await slow.Entered.Task;
            Assert.True(navigation.Back());
            slow.Complete(RouteLoadResult.Available);
            Assert.Equal(NavigationOutcome.Stale, await pending);
            Assert.Same(home, navigation.Current);
            Assert.Equal(0, navigation.HistoryCount);
            Assert.False(navigation.Back());
        }

        [Fact]
        public async Task DisposeAndReturnHomeDuringDelayedLoadDiscardTheLateRoute()
        {
            var home = new HomeRoute();
            var navigation = new NavigationCoordinator(home);
            var slow = new DeferredLoader(ignoreCancellation: true);
            var pending = navigation.NavigateAsync(new PlayRoute(new SemanticId("late"), 1), NavigationMode.Push, slow, CancellationToken.None);
            await slow.Entered.Task;
            navigation.ReturnHome();
            slow.Complete(RouteLoadResult.Available);
            Assert.Equal(NavigationOutcome.Stale, await pending);
            Assert.Same(home, navigation.Current);

            var second = new DeferredLoader(ignoreCancellation: true);
            var disposedPending = navigation.NavigateAsync(new PlayRoute(new SemanticId("late-2"), 2), NavigationMode.Push, second, CancellationToken.None);
            await second.Entered.Task;
            navigation.Dispose();
            second.Complete(RouteLoadResult.Available);
            Assert.Equal(NavigationOutcome.Stale, await disposedPending);
            Assert.Same(home, navigation.Current);
        }

        private sealed class ImmediateLoader : IRouteLoader
        {
            public Task<RouteLoadResult> LoadAsync(NavigationRoute route, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(RouteLoadResult.Available);
            }
        }

        private sealed class DeferredLoader : IRouteLoader
        {
            private readonly bool ignoreCancellation;
            private readonly TaskCompletionSource<RouteLoadResult> completion = new TaskCompletionSource<RouteLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            public DeferredLoader(bool ignoreCancellation) { this.ignoreCancellation = ignoreCancellation; }
            public TaskCompletionSource<bool> Entered { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public async Task<RouteLoadResult> LoadAsync(NavigationRoute route, CancellationToken cancellationToken)
            {
                Entered.TrySetResult(true);
                if (ignoreCancellation) return await completion.Task.ConfigureAwait(false);
                var cancelled = new TaskCompletionSource<RouteLoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(() => cancelled.TrySetCanceled(cancellationToken)))
                    return await await Task.WhenAny(completion.Task, cancelled.Task).ConfigureAwait(false);
            }
            public void Complete(RouteLoadResult result) => completion.TrySetResult(result);
        }
    }
}
