using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Navigation.Kernel;
using Outcome = GamePlatform.Navigation.Kernel.NavigationOutcome;

namespace GamePlatform.Tests.Navigation
{
    public sealed class NavigationKernelTests
    {
        private static readonly NavigationDefinition<string, string> Screen = new NavigationDefinition<string, string>("screen");
        private static readonly NavigationDefinition<string, bool> Modal = new NavigationDefinition<string, bool>("confirmation", NavigationPresentation.Modal);
        private static NavigationRequest<string, string> Request(string argument) => new NavigationRequest<string, string>(Screen, argument);
        private static NavigationRequest<string, bool> Dialog(string argument) => new NavigationRequest<string, bool>(Modal, argument);

        [Fact]
        public void AsynchronousAdmissionCommitsOnTheCallingSynchronizationContext()
        {
            SynchronizationContext? previous = SynchronizationContext.Current;
            using var context = new PumpContext();
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                using var kernel = new NavigationKernel("single-threaded-host");
                var admission = new TaskCompletionSource<NavigationAdmissionOutcome>();
                SynchronizationContext? committedOn = null;
                kernel.SnapshotChanged += snapshot =>
                {
                    if (snapshot.Pending.Count == 0 && snapshot.Entries.Count == 1)
                        committedOn = SynchronizationContext.Current;
                };
                Task<NavigationOpenResult<string>> opening = kernel.SelectRootAsync(Request("async"), (_, _) => admission.Task);
                Assert.False(opening.IsCompleted);
                admission.SetResult(NavigationAdmissionOutcome.Available);
                context.PumpUntil(opening);
                Assert.True(opening.IsCompletedSuccessfully);
                Assert.Single(kernel.Snapshot.Entries);
                Assert.Same(context, committedOn);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PendingJoinAndCancellationSettleWhileTheHostContextIsPumped(bool cancelJoin)
        {
            SynchronizationContext? previous = SynchronizationContext.Current;
            using var context = new PumpContext();
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                using var kernel = new NavigationKernel("single-threaded-host");
                kernel.SelectRootAsync(Request("root"));
                var admission = new TaskCompletionSource<NavigationAdmissionOutcome>();
                using var cancellation = new CancellationTokenSource();
                var definition = new NavigationDefinition<string, string>("joined", multiplicity: NavigationMultiplicity.ReuseMatching);
                var request = new NavigationRequest<string, string>(definition, "same", "same");
                Task<NavigationOpenResult<string>> opening = kernel.PushAsync(request, (_, _) => admission.Task);
                Task<NavigationOpenResult<string>> joined = kernel.PushAsync(request, cancellationToken: cancellation.Token);
                NavigationOutcome? joinedOutcome = null;
                async Task ObserveJoin()
                {
                    joinedOutcome = (await joined).Outcome;
                }
                Task observed = ObserveJoin();
                if (cancelJoin)
                {
                    cancellation.Cancel();
                    context.PumpUntil(observed);
                    Assert.Equal(Outcome.Cancelled, joinedOutcome);
                    Assert.Single(kernel.Snapshot.Pending);
                }
                admission.SetResult(NavigationAdmissionOutcome.Available);
                context.PumpUntil(Task.WhenAll(opening, observed));
                Assert.Equal(cancelJoin ? Outcome.Cancelled : Outcome.Reused, joinedOutcome);
                Assert.Equal(2, kernel.Snapshot.Entries.Count);
                Assert.Empty(kernel.Snapshot.Pending);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }

        private sealed class PumpContext : SynchronizationContext, IDisposable
        {
            private readonly BlockingCollection<Action> callbacks = new BlockingCollection<Action>();
            public override void Post(SendOrPostCallback callback, object? state) => callbacks.Add(() => callback(state));
            public void PumpUntil(Task task)
            {
                while (!task.IsCompleted)
                {
                    Assert.True(callbacks.TryTake(out Action? callback, TimeSpan.FromSeconds(5)), "No caller-context continuation was posted.");
                    callback!();
                }
            }
            public void Dispose() => callbacks.Dispose();
        }

        [Fact]
        public void CancelledAdmissionObserverCanReenterBeforeLateProviderFault()
        {
            SynchronizationContext? previous = SynchronizationContext.Current;
            using var context = new PumpContext();
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                using var kernel = new NavigationKernel("reentrant-cancellation");
                kernel.SelectRootAsync(Request("root"));
                var provider = new TaskCompletionSource<NavigationAdmissionOutcome>();
                using var caller = new CancellationTokenSource();
                Task<NavigationOpenResult<string>> opening = kernel.PushAsync(Request("pending"),
                    (_, _) => provider.Task, caller.Token);
                async Task Observe()
                {
                    Assert.Equal(Outcome.Cancelled, (await opening).Outcome);
                    Assert.Same(context, SynchronizationContext.Current);
                    Assert.Empty(kernel.Snapshot.Pending);
                    Assert.Equal(Outcome.Applied, (await kernel.PushAsync(Request("replacement"))).Outcome);
                }
                Task observed = Observe();
                caller.Cancel();
                context.PumpUntil(observed);
                Assert.True(observed.IsCompletedSuccessfully, observed.Exception?.ToString());
                provider.SetException(new InvalidOperationException("Late noncooperative provider"));
                Assert.Equal("replacement", kernel.Snapshot.Current!.Entry.GetArguments<string>());
                Assert.Empty(kernel.Snapshot.Pending);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }

        [Fact]
        public async Task RepeatedTypesRestoreExactCallerChainAndSnapshotsRemainStable()
        {
            using var kernel = new NavigationKernel("account", generation: 7);
            var root = await kernel.SelectRootAsync(Request("Ranks"));
            var ids = new List<EntryId> { root.Entry!.Id };
            foreach (var subject in new[] { "A", "X", "B", "Y", "A" })
            {
                var current = kernel.Snapshot.Current!.Entry;
                kernel.SaveRestorationState(current.Id, "scroll:" + current.GetArguments<string>());
                var result = await kernel.PushAsync(Request(subject));
                ids.Add(result.Entry!.Id);
                Assert.Equal(root.Entry.FlowId, result.Entry.FlowId);
                Assert.Equal(7, result.Entry.Generation);
            }
            Assert.Equal(ids.Count, ids.Distinct().Count());
            var previous = kernel.Snapshot;
            foreach (var expected in new[] { "Y", "B", "X", "A", "Ranks" })
            {
                Assert.Equal(Outcome.Applied, kernel.Back());
                Assert.Equal(expected, kernel.Snapshot.Current!.Entry.GetArguments<string>());
                Assert.Equal("scroll:" + expected, kernel.Snapshot.Current.RestorationState);
            }
            Assert.Equal(6, previous.Entries.Count);
            Assert.Equal("A", previous.Current!.Entry.GetArguments<string>());
            Assert.Equal(NavigationEntryState.Active, previous.Current.State);
            Assert.Equal(Outcome.NoChange, kernel.Back());
        }

        [Fact]
        public async Task CapacityRejectsWithoutCallingProviderOrEvictingCaller()
        {
            using var kernel = new NavigationKernel("account", capacity: 2);
            await kernel.SelectRootAsync(Request("root"));
            var caller = await kernel.PushAsync(Request("A"));
            var invoked = false;
            var result = await kernel.PushAsync(Request("B"), (_, __) => { invoked = true; return Task.FromResult(NavigationAdmissionOutcome.Available); });
            Assert.Equal(Outcome.CapacityExceeded, result.Outcome);
            Assert.False(invoked);
            Assert.Equal(caller.Entry!.Id, kernel.Snapshot.Current!.Entry.Id);
            Assert.Equal(NavigationChildStatus.Cancelled, (await result.Result).Status);
            Assert.Equal(Outcome.Applied, kernel.Back());
            Assert.Equal("root", kernel.Snapshot.Current!.Entry.GetArguments<string>());
        }

        [Fact]
        public async Task RootSelectionRetiresFlowAndDoesNotBuildTabHistory()
        {
            using var kernel = new NavigationKernel("account");
            var root = await kernel.SelectRootAsync(Request("Ranks"));
            var child = await kernel.PushAsync(Request("A"));
            var modal = await kernel.OpenModalAsync(child.Entry!.Id, Dialog("confirm"));
            var next = await kernel.SelectRootAsync(Request("Teams"));
            Assert.NotEqual(root.Entry!.FlowId, next.Entry!.FlowId);
            Assert.True(root.Entry.LifetimeToken.IsCancellationRequested);
            Assert.True(child.Entry.LifetimeToken.IsCancellationRequested);
            Assert.True(modal.Entry!.LifetimeToken.IsCancellationRequested);
            Assert.Equal(NavigationChildStatus.OwnerRetired, (await child.Result).Status);
            Assert.Single(kernel.Snapshot.Entries);
            Assert.Empty(kernel.Snapshot.Modals);
            Assert.Equal(Outcome.NoChange, kernel.Back());
        }

        [Fact]
        public async Task NestedModalsHaveTypedCallerResultsAndRetireTopDown()
        {
            using var kernel = new NavigationKernel("account");
            var root = await kernel.SelectRootAsync(Request("root"));
            var first = await kernel.OpenModalAsync(root.Entry!.Id, Dialog("one"));
            var second = await kernel.OpenModalAsync(first.Entry!.Id, Dialog("two"));
            Assert.Equal(2, kernel.Snapshot.Modals.Count);
            Assert.Equal(Outcome.InvalidRequest, kernel.Complete(second.Entry!.Id, "wrong-type"));
            Assert.Equal(Outcome.Applied, kernel.Complete(second.Entry.Id, true));
            Assert.True((await second.Result).HasValue);
            Assert.True((await second.Result).Value);
            Assert.Equal(first.Entry.Id, kernel.Snapshot.Top!.Entry.Id);
            var third = await kernel.OpenModalAsync(first.Entry.Id, Dialog("three"));
            var retired = new List<string>();
            first.Entry.LifetimeToken.Register(() => retired.Add("parent"));
            third.Entry!.LifetimeToken.Register(() => retired.Add("child"));
            Assert.Equal(Outcome.Applied, kernel.Close(first.Entry.Id));
            Assert.Equal(new[] { "child", "parent" }, retired);
            Assert.Equal(NavigationChildStatus.OwnerRetired, (await third.Result).Status);
            Assert.Equal(Outcome.NoChange, kernel.Complete(third.Entry.Id, true));
            Assert.False((await third.Result).HasValue);
        }

        [Fact]
        public async Task BackCancelsPendingPromptlyEvenWhenProviderIgnoresToken()
        {
            using var kernel = new NavigationKernel("account");
            await kernel.SelectRootAsync(Request("root"));
            var delayed = new DelayedAdmission();
            var task = kernel.PushAsync(Request("late"), delayed.Admit);
            var provisional = await delayed.Entered.Task;
            Assert.Equal(NavigationEntryState.Provisional, kernel.Snapshot.Pending.Single().State);
            Assert.Equal(Outcome.Applied, kernel.Back());
            Assert.Equal(Outcome.Cancelled, (await task.WaitAsync(TimeSpan.FromSeconds(5))).Outcome);
            Assert.True(provisional.LifetimeToken.IsCancellationRequested);
            delayed.Resolve();
            Assert.Equal("root", kernel.Snapshot.Current!.Entry.GetArguments<string>());
            Assert.Empty(kernel.Snapshot.Pending);
        }

        [Fact]
        public async Task SameSlotSupersedesWhileIndependentModalAndScreenAdmissionsRemainAlive()
        {
            using var kernel = new NavigationKernel("account");
            var root = await kernel.SelectRootAsync(Request("root"));
            var first = new DelayedAdmission();
            var second = new DelayedAdmission();
            var modal = new DelayedAdmission();
            var earlier = kernel.PushAsync(Request("old"), first.Admit);
            var dialog = kernel.OpenModalAsync(root.Entry!.Id, Dialog("dialog"), modal.Admit);
            Assert.Equal(2, kernel.Snapshot.Pending.Count);
            var later = kernel.PushAsync(Request("new"), second.Admit);
            Assert.Equal(Outcome.Stale, (await earlier.WaitAsync(TimeSpan.FromSeconds(5))).Outcome);
            Assert.False((await modal.Entered.Task).LifetimeToken.IsCancellationRequested);
            modal.Resolve();
            Assert.Equal(Outcome.Applied, (await dialog).Outcome);
            second.Resolve();
            Assert.Equal(Outcome.Applied, (await later).Outcome);
            first.Resolve();
            Assert.Single(kernel.Snapshot.Modals);
            Assert.Equal("new", kernel.Snapshot.Current!.Entry.GetArguments<string>());
        }

        [Fact]
        public async Task RequestCancellationStopsOnlyPrecommitWorkAndScopeRetirementFencesLateAdmission()
        {
            using var kernel = new NavigationKernel("account", 4);
            using var token = new CancellationTokenSource();
            var root = await kernel.SelectRootAsync(Request("root"), cancellationToken: token.Token);
            token.Cancel();
            Assert.False(root.Entry!.LifetimeToken.IsCancellationRequested);
            var delayed = new DelayedAdmission();
            var pending = kernel.PushAsync(Request("late"), delayed.Admit);
            var provisional = await delayed.Entered.Task;
            Assert.Equal(Outcome.Applied, kernel.RetireScope(5));
            Assert.Equal(Outcome.Retired, (await pending.WaitAsync(TimeSpan.FromSeconds(5))).Outcome);
            Assert.True(provisional.LifetimeToken.IsCancellationRequested);
            delayed.Resolve();
            var next = await kernel.SelectRootAsync(Request("fresh"));
            Assert.Equal(5, next.Entry!.Generation);
            Assert.Single(kernel.Snapshot.Entries);
        }

        [Fact]
        public async Task FailedOrUnavailableAdmissionsRetireProvisionalAndSettleResults()
        {
            using var kernel = new NavigationKernel("account");
            await kernel.SelectRootAsync(Request("root"));
            NavigationEntry? rejected = null;
            var failed = await kernel.PushAsync(Request("failure"), (entry, _) =>
            {
                rejected = entry;
                throw new InvalidOperationException("private details");
            });
            Assert.Equal(Outcome.Failed, failed.Outcome);
            Assert.True(rejected!.LifetimeToken.IsCancellationRequested);
            Assert.False((await failed.Result).HasValue);
            var unavailable = await kernel.PushAsync(Request("unavailable"), (_, __) => Task.FromResult(NavigationAdmissionOutcome.Unavailable));
            Assert.Equal(Outcome.Unavailable, unavailable.Outcome);
            Assert.False((await unavailable.Result).HasValue);
            Assert.Single(kernel.Snapshot.Entries);
        }

        [Fact]
        public async Task ObserversAndCancellationCallbacksDoNotRunUnderStateLockOrPreventCleanup()
        {
            using var kernel = new NavigationKernel("account");
            kernel.SnapshotChanged += _ => throw new InvalidOperationException("observer");
            var observed = 0;
            kernel.SnapshotChanged += _ =>
            {
                Assert.True(Task.Run(() => kernel.Snapshot).Wait(TimeSpan.FromSeconds(2)));
                observed++;
            };
            await kernel.SelectRootAsync(Request("root"));
            var child = await kernel.PushAsync(Request("A"));
            var callbackOutsideLock = false;
            child.Entry!.LifetimeToken.Register(() =>
            {
                callbackOutsideLock = Task.Run(() => kernel.Snapshot).Wait(TimeSpan.FromSeconds(2));
                throw new InvalidOperationException("cancel observer");
            });
            kernel.Close(child.Entry.Id);
            Assert.True(callbackOutsideLock);
            Assert.True(observed >= 5);
            Assert.Equal(NavigationChildStatus.Cancelled, (await child.Result).Status);
        }

        [Fact]
        public async Task ReuseFocusesOriginalEntryAndKeepsOriginalCallerOwnership()
        {
            using var kernel = new NavigationKernel("account");
            var root = await kernel.SelectRootAsync(Request("root"));
            var definition = new NavigationDefinition<string, string>("profile", multiplicity: NavigationMultiplicity.ReuseMatching);
            var request = new NavigationRequest<string, string>(definition, "A", "A");
            var first = await kernel.PushAsync(request);
            var child = await kernel.PushAsync(Request("team"));
            var reused = await kernel.PushAsync(request);
            Assert.Equal(Outcome.Reused, reused.Outcome);
            Assert.Equal(first.Entry!.Id, reused.Entry!.Id);
            Assert.Equal(root.Entry!.Id, reused.Entry.ParentId);
            Assert.Same(first.Result, reused.Result);
            Assert.True(child.Entry!.LifetimeToken.IsCancellationRequested);
            Assert.Equal(Outcome.Applied, kernel.Complete(first.Entry.Id, "answer"));
            Assert.Equal("answer", (await first.Result).Value);
        }

        [Fact]
        public async Task EntryHandlesCannotCloseAnotherKernelAndReturnRootKeepsOriginalIdentity()
        {
            using var first = new NavigationKernel("same", 0);
            using var second = new NavigationKernel("same", 0);
            var root = await first.SelectRootAsync(Request("root"));
            var foreign = await second.SelectRootAsync(Request("foreign"));
            Assert.Equal(Outcome.NoChange, first.Close(foreign.Entry!.Id));
            await first.PushAsync(Request("A"));
            Assert.Equal(Outcome.Applied, first.ReturnToRoot());
            Assert.Equal(root.Entry!.Id, first.Snapshot.Current!.Entry.Id);
            Assert.False(root.Entry.LifetimeToken.IsCancellationRequested);
        }

        [Fact]
        public async Task MatchingPendingEntryJoinsOnceAndJoiningCancellationDoesNotCancelOwner()
        {
            using var kernel = new NavigationKernel("account");
            await kernel.SelectRootAsync(Request("root"));
            var definition = new NavigationDefinition<string, string>("profile", multiplicity: NavigationMultiplicity.ReuseMatching);
            var request = new NavigationRequest<string, string>(definition, "A", "A");
            var delayed = new DelayedAdmission();
            var owner = kernel.PushAsync(request, delayed.Admit);
            var joined = kernel.PushAsync(request, (_, __) => throw new Exception("must join existing admission"));
            using var cancellation = new CancellationTokenSource();
            var cancelledJoin = kernel.PushAsync(request, cancellationToken: cancellation.Token);
            Assert.Single(kernel.Snapshot.Pending);
            Assert.False(joined.IsCompleted);
            cancellation.Cancel();
            Assert.Equal(Outcome.Cancelled, (await cancelledJoin).Outcome);
            Assert.False((await delayed.Entered.Task).LifetimeToken.IsCancellationRequested);
            delayed.Resolve();
            Assert.Equal(Outcome.Applied, (await owner).Outcome);
            Assert.Equal(Outcome.Reused, (await joined).Outcome);
            Assert.Same((await owner).Result, (await joined).Result);
        }

        [Fact]
        public async Task ReplacingOwnerRetiresPendingAndCommittedChildrenWithoutSuccessfulResults()
        {
            using var kernel = new NavigationKernel("account");
            await kernel.SelectRootAsync(Request("root"));
            var owner = await kernel.PushAsync(Request("A"));
            var first = await kernel.OpenModalAsync(owner.Entry!.Id, Dialog("first"));
            var delayed = new DelayedAdmission();
            var second = kernel.OpenModalAsync(first.Entry!.Id, Dialog("second"), delayed.Admit);
            var replacement = await kernel.ReplaceAsync(Request("B"));
            Assert.Equal(Outcome.Applied, replacement.Outcome);
            Assert.Equal(Outcome.Stale, (await second.WaitAsync(TimeSpan.FromSeconds(5))).Outcome);
            Assert.Equal(NavigationChildStatus.OwnerRetired, (await first.Result).Status);
            Assert.True(owner.Entry.LifetimeToken.IsCancellationRequested);
            Assert.Equal(Outcome.NoChange, kernel.Complete(first.Entry.Id, true));
            delayed.Resolve();
            Assert.Empty(kernel.Snapshot.Modals);
        }

        [Fact]
        public async Task ConcurrentCapacityIsRecheckedAtCommit()
        {
            using var kernel = new NavigationKernel("account", capacity: 2);
            var root = await kernel.SelectRootAsync(Request("root"));
            var delayed = new DelayedAdmission();
            var screen = kernel.PushAsync(Request("A"), delayed.Admit);
            await kernel.OpenModalAsync(root.Entry!.Id, Dialog("modal"));
            delayed.Resolve();
            Assert.Equal(Outcome.CapacityExceeded, (await screen).Outcome);
            Assert.Single(kernel.Snapshot.Entries);
            Assert.Single(kernel.Snapshot.Modals);
        }

        [Fact]
        public async Task ReentrantDescendantCleanupCannotDeliverResultToRetiredCaller()
        {
            using var kernel = new NavigationKernel("account");
            var root = await kernel.SelectRootAsync(Request("root"));
            var child = await kernel.PushAsync(Request("child"));
            var grandchild = await kernel.OpenModalAsync(child.Entry!.Id, Dialog("grandchild"));
            grandchild.Entry!.LifetimeToken.Register(() => kernel.Close(root.Entry!.Id));
            Assert.Equal(Outcome.Applied, kernel.Complete(child.Entry.Id, "must suppress"));
            Assert.Equal(NavigationChildStatus.OwnerRetired, (await child.Result).Status);
            Assert.False((await child.Result).HasValue);
        }

        [Fact]
        public async Task RepeatedModalIntentFromOriginalOwnerFocusesMatchingModal()
        {
            using var kernel = new NavigationKernel("account");
            var root = await kernel.SelectRootAsync(Request("root"));
            var definition = new NavigationDefinition<string, bool>("singleton-dialog", NavigationPresentation.Modal, NavigationMultiplicity.SingletonPerScope);
            var request = new NavigationRequest<string, bool>(definition, "dialog");
            var first = await kernel.OpenModalAsync(root.Entry!.Id, request);
            var reused = await kernel.OpenModalAsync(root.Entry.Id, request);
            Assert.Equal(Outcome.Reused, reused.Outcome);
            Assert.Equal(first.Entry!.Id, reused.Entry!.Id);
            Assert.Single(kernel.Snapshot.Modals);
        }

        [Fact]
        public async Task DelayedModalCannotCommitAfterIndependentScreenCoversItsOwner()
        {
            using var kernel = new NavigationKernel("account");
            var root = await kernel.SelectRootAsync(Request("root"));
            var delayed = new DelayedAdmission();
            var modal = kernel.OpenModalAsync(root.Entry!.Id, Dialog("old-owner"), delayed.Admit);
            var child = await kernel.PushAsync(Request("child"));
            Assert.False((await delayed.Entered.Task).LifetimeToken.IsCancellationRequested);
            delayed.Resolve();
            Assert.Equal(Outcome.Stale, (await modal).Outcome);
            Assert.Empty(kernel.Snapshot.Modals);
            Assert.Equal(child.Entry!.Id, kernel.Snapshot.Top!.Entry.Id);
        }

        private sealed class DelayedAdmission
        {
            private readonly TaskCompletionSource<NavigationAdmissionOutcome> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<NavigationEntry> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<NavigationAdmissionOutcome> Admit(NavigationEntry entry, CancellationToken token) { Entered.TrySetResult(entry); return result.Task; }
            public void Resolve() => result.TrySetResult(NavigationAdmissionOutcome.Available);
        }
    }
}
