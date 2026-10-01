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
            Assert.Throws<ArgumentOutOfRangeException>(() => new DetailRoute(new string('k', 129), new CallerReturnContext(new HomeRoute(), "home")));
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

        [Fact]
        public async Task LoaderExceptionPropagatesAndLeavesNoPhantomLoad()
        {
            var home = new HomeRoute();
            using var navigation = new NavigationCoordinator(home);
            var failing = new DeferredLoader(ignoreCancellation: true);
            var pending = navigation.NavigateAsync(new PlayRoute(new SemanticId("boom"), 1), NavigationMode.Push, failing, CancellationToken.None);
            await failing.Entered.Task;
            failing.Fail(new InvalidOperationException("load failed"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
            Assert.False(navigation.Back());

            await navigation.NavigateAsync(new DestinationRoute("packs"), NavigationMode.Push, new ImmediateLoader(), CancellationToken.None);
            var failingAgain = new DeferredLoader(ignoreCancellation: true);
            var second = navigation.NavigateAsync(new PlayRoute(new SemanticId("boom-2"), 2), NavigationMode.Push, failingAgain, CancellationToken.None);
            await failingAgain.Entered.Task;
            failingAgain.Fail(new InvalidOperationException("load failed"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => second);
            Assert.True(navigation.Back());
            Assert.Same(home, navigation.Current);
        }

        [Fact]
        public async Task CancellationIgnoringLoaderAfterCallerCancelClearsLoading()
        {
            var home = new HomeRoute();
            using var navigation = new NavigationCoordinator(home);
            using var caller = new CancellationTokenSource();
            var slow = new DeferredLoader(ignoreCancellation: true);
            var pending = navigation.NavigateAsync(new PlayRoute(new SemanticId("late"), 1), NavigationMode.Push, slow, caller.Token);
            await slow.Entered.Task;
            caller.Cancel();
            slow.Complete(RouteLoadResult.Available);
            Assert.Equal(NavigationOutcome.Stale, await pending);
            Assert.Same(home, navigation.Current);
            Assert.False(navigation.Back());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task OlderOperationFinishingDoesNotClearNewerLoad(bool olderFails)
        {
            var home = new HomeRoute();
            using var navigation = new NavigationCoordinator(home);
            var older = new DeferredLoader(ignoreCancellation: true);
            var newer = new DeferredLoader(ignoreCancellation: true);
            var olderPending = navigation.NavigateAsync(new PlayRoute(new SemanticId("a"), 1), NavigationMode.Push, older, CancellationToken.None);
            await older.Entered.Task;
            var newerPending = navigation.NavigateAsync(new PlayRoute(new SemanticId("b"), 2), NavigationMode.Push, newer, CancellationToken.None);
            await newer.Entered.Task;
            if (olderFails)
            {
                older.Fail(new InvalidOperationException("older failed"));
                await Assert.ThrowsAsync<InvalidOperationException>(() => olderPending);
            }
            else
            {
                older.Complete(RouteLoadResult.Available);
                Assert.Equal(NavigationOutcome.Stale, await olderPending);
            }
            Assert.True(navigation.Back());
            newer.Complete(RouteLoadResult.Available);
            Assert.Equal(NavigationOutcome.Stale, await newerPending);
            Assert.Same(home, navigation.Current);
            Assert.False(navigation.Back());
        }

        [Fact]
        public async Task BackIssuedInsideLoadThatThenReturnsAvailableYieldsStaleAndKeepsCurrent()
        {
            var home = new HomeRoute();
            using var navigation = new NavigationCoordinator(home);
            var loader = new BackingLoader(navigation);
            Assert.Equal(NavigationOutcome.Stale, await navigation.NavigateAsync(new PlayRoute(new SemanticId("raced"), 1), NavigationMode.Push, loader, CancellationToken.None));
            Assert.True(loader.BackResult);
            Assert.Same(home, navigation.Current);
            Assert.Equal(0, navigation.HistoryCount);
            Assert.False(navigation.Back());
        }

        [Fact]
        public async Task BackAfterAppliedNavigationPopsHistoryWithoutPhantomCancel()
        {
            var home = new HomeRoute();
            using var navigation = new NavigationCoordinator(home);
            Assert.Equal(NavigationOutcome.Applied, await navigation.NavigateAsync(new DestinationRoute("packs"), NavigationMode.Push, new ImmediateLoader(), CancellationToken.None));
            Assert.True(navigation.Back());
            Assert.Same(home, navigation.Current);
            Assert.False(navigation.Back());
        }

        private sealed class BackingLoader : IRouteLoader
        {
            private readonly NavigationCoordinator navigation;
            public BackingLoader(NavigationCoordinator navigation) { this.navigation = navigation; }
            public bool BackResult { get; private set; }
            public Task<RouteLoadResult> LoadAsync(NavigationRoute route, CancellationToken cancellationToken)
            {
                BackResult = navigation.Back();
                return Task.FromResult(RouteLoadResult.Available);
            }
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
            public void Fail(Exception exception) => completion.TrySetException(exception);
        }
    }
}
