using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Navigation.Kernel.Flows;

namespace GamePlatform.Tests.UiFlows
{
    public sealed class FlowHardeningTests
    {
        [Fact]
        public async Task TutorialDisposedReentrantlyOrBeforeOperationReturnsRetired()
        {
            var store = new Store();
            var flow = new TutorialFlow(Identity(), store, () => true);
            store.Load = () =>
            {
                flow.Dispose();
                return Task.FromResult<TutorialCheckpoint?>(null);
            };
            Assert.Equal(TutorialFlowOutcome.Retired, (await flow.LoadAsync()).Outcome);
            Assert.Equal(TutorialFlowOutcome.Retired, (await flow.LoadAsync()).Outcome);
            Assert.Equal(TutorialFlowOutcome.Retired, (await flow.CompleteAsync("final")).Outcome);
        }

        [Fact]
        public void ReentrantPromptClaimCannotClaimASecondItem()
        {
            var scheduler = Prompts();
            PromptClaim? inner = null;
            bool reentered = false;
            bool outer = scheduler.TryClaimNext(DateTimeOffset.UnixEpoch, default, (_, _) =>
            {
                if (!reentered)
                {
                    reentered = true;
                    Assert.True(scheduler.TryClaimNext(DateTimeOffset.UnixEpoch, default, (_, _) => true, out inner));
                }
                return true;
            }, out _);
            Assert.NotNull(inner);
            Assert.False(outer);
        }

        [Fact]
        public async Task ConcurrentPromptRevalidationCannotCommitTwoClaims()
        {
            var scheduler = Prompts();
            using var both = new Barrier(2);
            Task<bool> Claim() => Task.Run(() =>
            {
                bool first = true;
                return scheduler.TryClaimNext(DateTimeOffset.UnixEpoch, default, (_, _) =>
                {
                    if (first)
                    {
                        first = false;
                        Assert.True(both.SignalAndWait(TimeSpan.FromSeconds(5)));
                    }
                    return true;
                }, out _);
            });
            bool[] results = await Task.WhenAll(Claim(), Claim());
            Assert.Single(results, value => value);
        }

        [Fact]
        public void ExpiryDoesNotRevokeAPresentedClaimAndOldTokensStayInvalid()
        {
            var scheduler = Prompts();
            Assert.True(scheduler.TryClaimNext(DateTimeOffset.UnixEpoch, default, (_, _) => true, out var first));
            Assert.False(scheduler.TryClaimNext(DateTimeOffset.UnixEpoch.AddHours(2), default, (_, _) => true, out _));
            Assert.Equal(1, scheduler.PendingCount);
            Assert.True(scheduler.ReleaseClaim(first!));
            Assert.False(scheduler.TryClaimNext(DateTimeOffset.UnixEpoch.AddHours(2), default, (_, _) => true, out _));
            Assert.Equal(0, scheduler.PendingCount);
            scheduler.Enqueue(new PromptRequest("A", 2, DateTimeOffset.UnixEpoch.AddHours(3)));
            Assert.True(scheduler.TryClaimNext(DateTimeOffset.UnixEpoch.AddHours(2), default, (_, _) => true, out var next));
            Assert.False(scheduler.ResolveClaim(first!));
            Assert.True(scheduler.ResolveClaim(next!));
        }

        [Fact]
        public async Task ReentrantCurrentLookupCannotUseItsStaleObservationToAdmitTwice()
        {
            var entries = new FeatureEntryDeduplicator<object>();
            object? current = null;
            int admissions = 0;
            Task<FeatureEntryResolution<object>>? nested = null;
            Task<object> Admit(string _, CancellationToken token)
            {
                admissions++;
                current = new object(); // Publish before completing, as required by the provider contract.
                return Task.FromResult(current);
            }
            var outer = await entries.ResolveAsync("scope:key", _ =>
            {
                object? stale = current;
                nested = entries.ResolveAsync("scope:key", _ => current, Admit, CancellationToken.None);
                return stale;
            }, Admit, CancellationToken.None);
            Assert.Same(outer.Handle, (await nested!).Handle);
            Assert.Equal(1, admissions);
        }

        [Fact]
        public async Task ConcurrentFeatureCallersShareOnePublishedHandle()
        {
            var entries = new FeatureEntryDeduplicator<object>();
            var admitted = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            object? current = null;
            int admissions = 0;
            Task<object> Admit(string _, CancellationToken token)
            {
                Interlocked.Increment(ref admissions);
                ready.TrySetResult(true);
                return admitted.Task;
            }
            Task<FeatureEntryResolution<object>>[] calls = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
                entries.ResolveAsync("scope:key", _ => Volatile.Read(ref current), Admit, CancellationToken.None))).ToArray();
            await ready.Task;
            var handle = new object();
            Volatile.Write(ref current, handle);
            admitted.SetResult(handle);
            foreach (var result in await Task.WhenAll(calls))
                Assert.Same(handle, result.Handle);
            Assert.Equal(1, admissions);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CancelledScopeCannotReuseCurrentOrPublishLookup(bool cancelDuringLookup)
        {
            using var scope = new CancellationTokenSource();
            if (!cancelDuringLookup)
                scope.Cancel();
            var entries = new FeatureEntryDeduplicator<object>();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => entries.ResolveAsync("scope:key", _ =>
            {
                scope.Cancel();
                return new object();
            }, (_, _) => throw new InvalidOperationException("Must not admit"), scope.Token));
        }

        [Fact]
        public void DelayedTutorialLoadSaveAndQueuedStoreCallsKeepTheCallerContext()
        {
            WithContext(context =>
            {
                var load = new TaskCompletionSource<TutorialCheckpoint?>();
                var save = new TaskCompletionSource<TutorialCheckpointSaveResult>();
                bool wrongContext = false;
                var store = new Store { Load = () => load.Task, Save = _ => save.Task };
                using var flow = new TutorialFlow(Identity(), store, () =>
                {
                    wrongContext |= !ReferenceEquals(context, SynchronizationContext.Current);
                    return true;
                });
                var loading = flow.LoadAsync();
                var saving = flow.CompleteAsync("final");
                Assert.False(loading.IsCompleted);
                Assert.Null(store.Requested);
                load.SetResult(null);
                context.PumpUntil(loading);
                context.PumpUntil(store.SaveStarted.Task);
                Assert.False(saving.IsCompleted);
                save.SetResult(TutorialCheckpointSaveResult.Acknowledge(store.Requested!));
                context.PumpUntil(saving);
                Assert.True(flow.IsCompleted);
                Assert.False(wrongContext);
            });
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void TutorialCancellationSettlesBeforeNoncooperativeStoreAndExercisesLateFailure(bool retire, bool loading)
        {
            WithContext(context =>
            {
                var read = new TaskCompletionSource<TutorialCheckpoint?>();
                var write = new TaskCompletionSource<TutorialCheckpointSaveResult>();
                var store = new Store { Load = () => read.Task, Save = _ => write.Task };
                using var caller = new CancellationTokenSource();
                using var flow = new TutorialFlow(Identity(), store, () => true);
                var operation = loading ? flow.LoadAsync(caller.Token) : flow.CompleteAsync("final", caller.Token);
                if (retire)
                    flow.Retire();
                else
                    caller.Cancel();
                context.PumpUntil(operation);
                Assert.Equal(retire ? TutorialFlowOutcome.Retired : TutorialFlowOutcome.Cancelled, operation.GetAwaiter().GetResult().Outcome);
                Assert.False(flow.IsCompleted);
                Task? queued = null;
                if (!retire)
                {
                    queued = flow.SaveCheckpointAsync("next");
                    Assert.Equal(loading ? 0 : 1, store.SaveCalls);
                }
                var drained = new TaskCompletionSource<bool>();
                // Exercise late faults; the sentinel pumps queued work, not an assertion that all
                // asynchronously scheduled private observers have drained.
                if (loading)
                    read.SetException(new InvalidOperationException("late read"));
                if (!loading || !retire)
                    write.SetException(new InvalidOperationException("late write"));
                context.Post(_ => drained.SetResult(true), null);
                context.PumpUntil(drained.Task);
                if (queued != null)
                {
                    // The next operation runs only after the old store task has settled.
                    context.PumpUntil(queued);
                    Assert.True(queued.IsFaulted);
                    _ = queued.Exception;
                }
                Assert.Null(flow.Checkpoint);
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DelayedAdmissionJoinCancellationAndLateFaultArePumped(bool retire)
        {
            WithContext(context =>
            {
                var entries = new FeatureEntryDeduplicator<object>();
                var admission = new TaskCompletionSource<object>();
                using var scope = new CancellationTokenSource();
                using var caller = new CancellationTokenSource();
                int calls = 0;
                var owner = entries.ResolveAsync("scope:key", _ => null, (_, _) => { calls++; return admission.Task; }, scope.Token);
                var join = entries.ResolveAsync("scope:key", _ => null, (_, _) => throw new Exception(), scope.Token, caller.Token);
                if (retire)
                    scope.Cancel();
                else
                    caller.Cancel();
                context.PumpUntil(join);
                Assert.True(join.IsCanceled);
                if (retire)
                {
                    context.PumpUntil(owner);
                    Assert.True(owner.IsCanceled);
                    admission.SetException(new InvalidOperationException("late admission"));
                }
                else
                {
                    Assert.False(owner.IsCompleted);
                    admission.SetResult(new object());
                    context.PumpUntil(owner);
                    Assert.True(owner.IsCompletedSuccessfully);
                }
                Assert.Equal(1, calls);
                var drained = new TaskCompletionSource<bool>();
                context.Post(_ => drained.SetResult(true), null);
                context.PumpUntil(drained.Task);
            });
        }

        [Fact]
        public void CancelledSaveAcknowledgementCannotCompleteTutorialAndNextWriteWaits()
        {
            WithContext(context =>
            {
                var pending = new TaskCompletionSource<TutorialCheckpointSaveResult>();
                var store = new Store { Save = _ => pending.Task };
                using var flow = new TutorialFlow(Identity(), store, () => true);
                using var caller = new CancellationTokenSource();
                var saving = flow.CompleteAsync("final", caller.Token);
                var old = store.Requested!;
                caller.Cancel();
                context.PumpUntil(saving);
                Assert.Equal(TutorialFlowOutcome.Cancelled, saving.GetAwaiter().GetResult().Outcome);
                store.Save = checkpoint => Task.FromResult(TutorialCheckpointSaveResult.Acknowledge(checkpoint));
                var next = flow.SaveCheckpointAsync("next");
                Assert.Equal(1, store.SaveCalls);
                pending.SetResult(TutorialCheckpointSaveResult.Acknowledge(old));
                context.PumpUntil(next);
                Assert.Equal(TutorialFlowOutcome.Acknowledged, next.GetAwaiter().GetResult().Outcome);
                Assert.Equal("next", flow.Checkpoint!.StepKey);
                Assert.False(flow.IsCompleted);
                Assert.Equal(2, store.SaveCalls);
            });
        }

        [Fact]
        public void SuccessfulPendingJoinAndScopeReplacementUseTheCorrectReservation()
        {
            WithContext(context =>
            {
                var entries = new FeatureEntryDeduplicator<object>();
                var oldProvider = new TaskCompletionSource<object>();
                using var oldScope = new CancellationTokenSource();
                object? current = null;
                var owner = entries.ResolveAsync("generation:key", _ => current, (_, _) => oldProvider.Task, oldScope.Token);
                var joined = entries.ResolveAsync("generation:key", _ => current, (_, _) => throw new Exception(), oldScope.Token);
                var handle = new object();
                current = handle;
                oldProvider.SetResult(handle);
                context.PumpUntil(Task.WhenAll(owner, joined));
                Assert.Same(handle, joined.GetAwaiter().GetResult().Handle);
                Assert.Equal(FeatureEntryResolutionKind.JoinedPendingAdmission, joined.GetAwaiter().GetResult().Kind);

                current = null;
                oldProvider = new TaskCompletionSource<object>();
                var retired = entries.ResolveAsync("generation:key", _ => current, (_, _) => oldProvider.Task, oldScope.Token);
                oldScope.Cancel();
                var freshProvider = new TaskCompletionSource<object>();
                var fresh = entries.ResolveAsync("generation:key", _ => current, (_, _) => freshProvider.Task, CancellationToken.None);
                context.PumpUntil(retired);
                Assert.True(retired.IsCanceled);
                oldProvider.SetException(new InvalidOperationException("old provider"));
                var newJoin = entries.ResolveAsync("generation:key", _ => current, (_, _) => throw new Exception(), CancellationToken.None);
                current = new object();
                freshProvider.SetResult(current);
                context.PumpUntil(Task.WhenAll(fresh, newJoin));
                Assert.Same(current, newJoin.GetAwaiter().GetResult().Handle);
                Assert.Equal(FeatureEntryResolutionKind.JoinedPendingAdmission, newJoin.GetAwaiter().GetResult().Kind);
            });
        }

        private static PromptScheduler Prompts()
        {
            var scheduler = new PromptScheduler();
            scheduler.Enqueue(new PromptRequest("A", 2, DateTimeOffset.UnixEpoch.AddHours(1)));
            scheduler.Enqueue(new PromptRequest("B", 1, DateTimeOffset.UnixEpoch.AddHours(1)));
            return scheduler;
        }

        private static TutorialCheckpointIdentity Identity() => new TutorialCheckpointIdentity("intro", "v1", TutorialScopeKind.Account, "fixture");

        private static void WithContext(Action<PumpContext> action)
        {
            SynchronizationContext? previous = SynchronizationContext.Current;
            var context = new PumpContext();
            SynchronizationContext.SetSynchronizationContext(context);
            try
            { action(context); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }

        private sealed class PumpContext : SynchronizationContext
        {
            private readonly BlockingCollection<Action> _callbacks = new BlockingCollection<Action>();
            public override void Post(SendOrPostCallback callback, object? state) => _callbacks.Add(() => callback(state));
            public void PumpUntil(Task task)
            {
                while (!task.IsCompleted)
                {
                    Assert.True(_callbacks.TryTake(out Action? callback, TimeSpan.FromSeconds(2)), "No caller-context continuation was posted.");
                    callback!();
                }
            }
            // A detached observer may legitimately post after the public operation settles.
            // Keep the queue available until the context is collected, like the real host context.
        }

        private sealed class Store : ITutorialCheckpointStore
        {
            public Func<Task<TutorialCheckpoint?>> Load { get; set; } = () => Task.FromResult<TutorialCheckpoint?>(null);
            public Func<TutorialCheckpoint, Task<TutorialCheckpointSaveResult>> Save { get; set; } = _ => throw new InvalidOperationException("late fixture");
            public TutorialCheckpoint? Requested { get; private set; }
            public int SaveCalls { get; private set; }
            public TaskCompletionSource<bool> SaveStarted { get; } = new TaskCompletionSource<bool>();
            public Task<TutorialCheckpoint?> LoadAsync(TutorialCheckpointIdentity identity, CancellationToken cancellationToken) => Load();
            public Task<TutorialCheckpointSaveResult> SaveAsync(TutorialCheckpoint checkpoint, CancellationToken cancellationToken)
            {
                Requested = checkpoint;
                SaveCalls++;
                SaveStarted.TrySetResult(true);
                return Save(checkpoint);
            }
        }
    }
}
