using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Navigation.Kernel.Flows;

namespace GamePlatform.Tests.UiFlows
{
    public sealed class TutorialFlowTests
    {
        [Fact]
        public async Task CompletionAndResumeAdvanceOnlyAfterMatchingAcknowledgement()
        {
            var identity = Identity();
            var store = new Store();
            using var flow = new TutorialFlow(identity, store, () => true);

            Assert.Equal(TutorialFlowOutcome.NoCheckpoint, (await flow.LoadAsync()).Outcome);
            store.Save = _ => Task.FromResult(TutorialCheckpointSaveResult.Reject());
            Assert.Equal(TutorialFlowOutcome.Rejected, (await flow.SaveCheckpointAsync("step.safe")).Outcome);
            Assert.Null(flow.Checkpoint);

            store.Save = checkpoint => Task.FromResult(TutorialCheckpointSaveResult.Acknowledge(checkpoint));
            TutorialFlowResult saved = await flow.SaveCheckpointAsync("step.safe");
            Assert.Equal(TutorialFlowOutcome.Acknowledged, saved.Outcome);
            Assert.Equal(1, saved.Checkpoint!.Revision);
            Assert.False(flow.IsCompleted);

            store.Loaded = saved.Checkpoint;
            using var resumed = new TutorialFlow(identity, store, () => true);
            Assert.Equal(TutorialFlowOutcome.Acknowledged, (await resumed.LoadAsync()).Outcome);
            store.Save = _ => Task.FromResult(TutorialCheckpointSaveResult.Reject());
            Assert.Equal(TutorialFlowOutcome.Rejected, (await resumed.CompleteAsync("step.final")).Outcome);
            Assert.False(resumed.IsCompleted);

            store.Save = checkpoint => Task.FromResult(TutorialCheckpointSaveResult.Acknowledge(checkpoint));
            TutorialFlowResult completed = await resumed.CompleteAsync("step.final");
            Assert.Equal(TutorialFlowOutcome.Acknowledged, completed.Outcome);
            Assert.True(resumed.IsCompleted);
        }

        [Fact]
        public async Task SkippingRemainsIncompleteAndWrongAcknowledgementIsRejected()
        {
            var identity = Identity();
            var store = new Store();
            using var flow = new TutorialFlow(identity, store, () => true);
            store.Save = checkpoint => Task.FromResult(TutorialCheckpointSaveResult.Acknowledge(
                new TutorialCheckpoint(checkpoint.Identity, "different", checkpoint.State, checkpoint.Revision)));

            Assert.Equal(TutorialFlowOutcome.Stale, (await flow.CompleteAsync("step.final")).Outcome);
            Assert.False(flow.IsCompleted);

            store.Save = checkpoint => Task.FromResult(TutorialCheckpointSaveResult.Acknowledge(checkpoint));
            Assert.Equal(TutorialFlowOutcome.Acknowledged, (await flow.SkipAsync("step.intro")).Outcome);
            Assert.Equal(TutorialCheckpointState.Skipped, flow.Checkpoint!.State);
            Assert.False(flow.IsCompleted);
        }

        [Fact]
        public async Task RetirementFencesLateStoreThatIgnoresCancellation()
        {
            var store = new Store();
            var pending = new TaskCompletionSource<TutorialCheckpointSaveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            store.Save = _ => pending.Task;
            using var scope = new CancellationTokenSource();
            var flow = new TutorialFlow(Identity(), store, () => true, scope.Token);
            Task<TutorialFlowResult> saving = flow.CompleteAsync("final");
            Assert.NotNull(store.LastToken);
            scope.Cancel();
            pending.SetResult(TutorialCheckpointSaveResult.Acknowledge(store.LastRequested!));

            TutorialFlowResult result = await saving;
            Assert.Equal(TutorialFlowOutcome.Retired, result.Outcome);
            Assert.Null(flow.Checkpoint);
            flow.Dispose();
        }

        [Fact]
        public async Task IndividualCancellationDoesNotPublishAnUnacknowledgedCompletion()
        {
            var store = new Store();
            var pending = new TaskCompletionSource<TutorialCheckpointSaveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            store.Save = _ => pending.Task;
            using var flow = new TutorialFlow(Identity(), store, () => true);
            using var caller = new CancellationTokenSource();
            Task<TutorialFlowResult> saving = flow.CompleteAsync("final", caller.Token);
            caller.Cancel();
            pending.SetResult(TutorialCheckpointSaveResult.Acknowledge(store.LastRequested!));

            Assert.Equal(TutorialFlowOutcome.Cancelled, (await saving).Outcome);
            Assert.False(flow.IsCompleted);
        }

        [Fact]
        public async Task RetiredScopeCannotApplyAReadThatCompletesLate()
        {
            var store = new Store();
            var pending = new TaskCompletionSource<TutorialCheckpoint?>(TaskCreationOptions.RunContinuationsAsynchronously);
            store.Load = (_, _) => pending.Task;
            var flow = new TutorialFlow(Identity(), store, () => true);
            Task<TutorialFlowResult> loading = flow.LoadAsync();
            flow.Dispose();
            pending.SetResult(new TutorialCheckpoint(Identity(), "resume", TutorialCheckpointState.InProgress, 1));

            Assert.Equal(TutorialFlowOutcome.Retired, (await loading).Outcome);
            Assert.Null(flow.Checkpoint);
        }

        private static TutorialCheckpointIdentity Identity() => new TutorialCheckpointIdentity(
            "tutorial.first-use", "v3", TutorialScopeKind.Event, "event:fixture-17/account:fixture-2");

        private sealed class Store : ITutorialCheckpointStore
        {
            public Func<TutorialCheckpointIdentity, CancellationToken, Task<TutorialCheckpoint?>> Load { get; set; } =
                (_, _) => Task.FromResult<TutorialCheckpoint?>(null);
            public Func<TutorialCheckpoint, Task<TutorialCheckpointSaveResult>> Save { get; set; } =
                checkpoint => Task.FromResult(TutorialCheckpointSaveResult.Acknowledge(checkpoint));
            public CancellationToken? LastToken { get; private set; }
            public TutorialCheckpoint? LastRequested { get; private set; }
            public TutorialCheckpoint? Loaded { get; set; }

            public Task<TutorialCheckpoint?> LoadAsync(TutorialCheckpointIdentity identity, CancellationToken cancellationToken)
            {
                LastToken = cancellationToken;
                if (Loaded != null)
                    return Task.FromResult<TutorialCheckpoint?>(Loaded);
                return Load(identity, cancellationToken);
            }

            public Task<TutorialCheckpointSaveResult> SaveAsync(TutorialCheckpoint checkpoint, CancellationToken cancellationToken)
            {
                LastToken = cancellationToken;
                LastRequested = checkpoint;
                return Save(checkpoint);
            }
        }
    }
}
