using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Presentation.Shared;
using GamePlatform.Navigation;

namespace GamePlatform.Tests.Navigation
{
    public sealed class Cl012NavigationAndPresenterTests
    {
        [Fact]
        public async Task MinimalHomePlayResultNextHomeFlowIsDeterministic()
        {
            using var navigation = new NavigationCoordinator(new HomeRoute());
            var loader = new ImmediateLoader();
            Assert.Equal(NavigationOutcome.Applied, await navigation.NavigateAsync(new PlayRoute(new SemanticId("level-1"), 1), NavigationMode.Push, loader, CancellationToken.None));
            Assert.Equal(NavigationOutcome.Applied, await navigation.NavigateAsync(new ResultRoute(new SemanticId("level-1"), CompletionPresentationState.Pending), NavigationMode.Push, loader, CancellationToken.None));
            Assert.Equal(NavigationOutcome.Applied, await navigation.NavigateAsync(new PlayRoute(new SemanticId("level-2"), 2), NavigationMode.Replace, loader, CancellationToken.None));
            var next = Assert.IsType<PlayRoute>(navigation.Current);
            Assert.Equal("level-2", next.ContentKey.Value);
            Assert.Equal(2, next.LevelScopeGeneration);
            Assert.Equal(2, navigation.HistoryCount);
            navigation.ReturnHome();
            Assert.IsType<HomeRoute>(navigation.Current);
            Assert.Equal(0, navigation.HistoryCount);
        }

        [Fact]
        public async Task ProfilePreservesEachCallerReturnContext()
        {
            var loader = new ImmediateLoader();
            foreach (var caller in new NavigationRoute[] { new HomeRoute(), new ResultRoute(new SemanticId("level-1"), CompletionPresentationState.Confirmed) })
            {
                using var navigation = new NavigationCoordinator(caller);
                var profile = new ProfileRoute(new CallerReturnContext(caller, caller.Kind + "-profile"));
                Assert.Equal(NavigationOutcome.Applied, await navigation.NavigateAsync(profile, NavigationMode.Push, loader, CancellationToken.None));
                Assert.Same(caller, Assert.IsType<ProfileRoute>(navigation.Current).ReturnContext.Route);
                Assert.True(navigation.Back());
                Assert.Same(caller, navigation.Current);
            }
        }

        [Fact]
        public void BackClosesActiveModalBeforeRouteAndOnlyItsOwnerCanCloseOnce()
        {
            var play = new PlayRoute(new SemanticId("level-1"), 1);
            using var navigation = new NavigationCoordinator(play);
            var first = navigation.OpenModal(new ModalRoute("pause"));
            var replacement = navigation.OpenModal(new ModalRoute("network"));
            Assert.False(navigation.CloseModal(first));
            Assert.True(navigation.CloseModal(replacement));
            Assert.False(navigation.CloseModal(replacement));
            var active = navigation.OpenModal(new ModalRoute("pause"));
            Assert.True(navigation.DeferModal(new ModalRoute("deferred")));
            Assert.False(navigation.TryOpenDeferredModal(out _));
            Assert.True(navigation.Back());
            Assert.Null(navigation.Modal);
            Assert.Same(play, navigation.Current);
            Assert.False(navigation.CloseModal(active));
            Assert.True(navigation.TryOpenDeferredModal(out var deferredOwner));
            Assert.Equal("deferred", navigation.Modal!.ModalKey);
            Assert.True(navigation.CloseModal(deferredOwner));
        }

        [Fact]
        public async Task CancelledOrSupersededLoadCannotPublishLate()
        {
            using var navigation = new NavigationCoordinator(new HomeRoute());
            var slow = new DeferredLoader(ignoreCancellation: true);
            var first = navigation.NavigateAsync(new PlayRoute(new SemanticId("late"), 1), NavigationMode.Push, slow, CancellationToken.None);
            await slow.Entered.Task;
            var current = navigation.NavigateAsync(new PlayRoute(new SemanticId("current"), 2), NavigationMode.Push, new ImmediateLoader(), CancellationToken.None);
            Assert.Equal(NavigationOutcome.Applied, await current);
            slow.Complete(RouteLoadResult.Available);
            Assert.Equal(NavigationOutcome.Stale, await first);
            Assert.Equal("current", Assert.IsType<PlayRoute>(navigation.Current).ContentKey.Value);

            using var cancellation = new CancellationTokenSource();
            var waiting = new DeferredLoader(ignoreCancellation: false);
            var cancelled = navigation.NavigateAsync(new ResultRoute(new SemanticId("current"), CompletionPresentationState.Pending), NavigationMode.Push, waiting, cancellation.Token);
            await waiting.Entered.Task;
            cancellation.Cancel();
            Assert.Equal(NavigationOutcome.Cancelled, await cancelled);
        }

        [Fact]
        public async Task UnavailableRouteAndBoundedHistoryPreserveValidState()
        {
            using var navigation = new NavigationCoordinator(new HomeRoute(), maximumHistory: 2);
            var unavailable = await navigation.NavigateAsync(new PlayRoute(new SemanticId("missing"), 1), NavigationMode.Push, new ImmediateLoader(RouteLoadResult.Unavailable), CancellationToken.None);
            Assert.Equal(NavigationOutcome.Unavailable, unavailable);
            Assert.IsType<HomeRoute>(navigation.Current);
            Assert.Equal(0, navigation.HistoryCount);
            await navigation.NavigateAsync(new PlayRoute(new SemanticId("one"), 1), NavigationMode.Push, new ImmediateLoader(), CancellationToken.None);
            await navigation.NavigateAsync(new PlayRoute(new SemanticId("two"), 2), NavigationMode.Push, new ImmediateLoader(), CancellationToken.None);
            await navigation.NavigateAsync(new PlayRoute(new SemanticId("three"), 3), NavigationMode.Push, new ImmediateLoader(), CancellationToken.None);
            Assert.Equal(2, navigation.HistoryCount);
        }

        [Fact]
        public void RepeatedBindUnbindRejectsStaleGenerationAndDisposesOwnedOnly()
        {
            var owner = Owner(generation: 4);
            var borrowed = new TrackedDisposable();
            var firstSubscription = new TrackedDisposable();
            var firstLease = new TrackedDisposable();
            var secondSubscription = new TrackedDisposable();
            var secondLease = new TrackedDisposable();
            using (var lifetime = new PresenterLifetime(owner, borrowed))
            {
                lifetime.Bind(firstSubscription, firstLease);
                lifetime.Bind(secondSubscription, secondLease);
                Assert.True(firstSubscription.Disposed);
                Assert.True(firstLease.Disposed);
                var published = 0;
                Assert.False(lifetime.TryPublish(Owner(generation: 5), () => published++));
                Assert.True(lifetime.TryPublish(owner, () => published++));
                Assert.Equal(1, published);
                lifetime.Unbind();
                lifetime.Unbind();
                Assert.True(secondSubscription.Disposed);
                Assert.True(secondLease.Disposed);
            }
            Assert.False(borrowed.Disposed);
        }

        [Fact]
        public void ResultPresentationDistinguishesPendingConfirmedAndSaveFailure()
        {
            var pending = new ResultPresentation(CompletionPresentationState.Pending, "pending_sync");
            var confirmed = new ResultPresentation(CompletionPresentationState.Confirmed, "confirmed");
            var failed = new ResultPresentation(CompletionPresentationState.SaveFailed, "save_failed");
            Assert.False(pending.CanAdvance);
            Assert.True(confirmed.CanAdvance);
            Assert.False(confirmed.CanRetrySave);
            Assert.True(failed.CanRetrySave);
        }

        private static ScopedOwnerContext Owner(long generation) => new ScopedOwnerContext(
            new OwnerScope(new BackendNamespace("test"), new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060")), new PlatformUserId(Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"))),
            new SemanticId("view"), generation);

        private sealed class ImmediateLoader : IRouteLoader
        {
            private readonly RouteLoadResult result;
            public ImmediateLoader(RouteLoadResult result = RouteLoadResult.Available) { this.result = result; }
            public Task<RouteLoadResult> LoadAsync(NavigationRoute route, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(result);
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

        private sealed class TrackedDisposable : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }
    }
}
