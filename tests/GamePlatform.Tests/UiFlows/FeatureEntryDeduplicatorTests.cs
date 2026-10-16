using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Navigation.Kernel.Flows;

namespace GamePlatform.Tests.UiFlows
{
    public sealed class FeatureEntryDeduplicatorTests
    {
        [Fact]
        public async Task RepeatedRequestsJoinOneAdmissionAndReuseTheCommittedHandle()
        {
            var deduplicator = new FeatureEntryDeduplicator<Handle>();
            var admitted = new TaskCompletionSource<Handle>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handle = new Handle();
            int calls = 0;
            Handle? current = null;
            Func<string, CancellationToken, Task<Handle>> admit = (_, _) => { calls++; return admitted.Task; };

            Task<FeatureEntryResolution<Handle>> first = deduplicator.ResolveAsync("profile:scope:A", _ => current, admit, CancellationToken.None);
            Task<FeatureEntryResolution<Handle>> second = deduplicator.ResolveAsync("profile:scope:A", _ => current, admit, CancellationToken.None);
            Assert.Equal(1, calls);
            current = handle;
            admitted.SetResult(handle);

            Assert.Equal(FeatureEntryResolutionKind.Admitted, (await first).Kind);
            Assert.Equal(FeatureEntryResolutionKind.JoinedPendingAdmission, (await second).Kind);
            FeatureEntryResolution<Handle> reused = await deduplicator.ResolveAsync("profile:scope:A", _ => current,
                (_, _) => throw new InvalidOperationException("A current entry should be reused."), CancellationToken.None);
            Assert.Equal(FeatureEntryResolutionKind.ReusedCurrent, reused.Kind);
            Assert.Same(handle, reused.Handle);
        }

        [Fact]
        public async Task CancellingOneJoinerDoesNotCancelSharedAdmission()
        {
            var deduplicator = new FeatureEntryDeduplicator<Handle>();
            var admitted = new TaskCompletionSource<Handle>(TaskCreationOptions.RunContinuationsAsynchronously);
            int calls = 0;
            Func<string, CancellationToken, Task<Handle>> admit = (_, token) => { calls++; Assert.False(token.IsCancellationRequested); return admitted.Task; };
            Task<FeatureEntryResolution<Handle>> owner = deduplicator.ResolveAsync("teams:scope:1", _ => null, admit, CancellationToken.None);
            using var caller = new CancellationTokenSource();
            Task<FeatureEntryResolution<Handle>> joiner = deduplicator.ResolveAsync("teams:scope:1", _ => null, admit,
                CancellationToken.None, caller.Token);
            caller.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await joiner);
            Assert.Equal(1, calls);
            var handle = new Handle();
            admitted.SetResult(handle);
            Assert.Same(handle, (await owner).Handle);
        }

        [Fact]
        public async Task ScopeRetirementSettlesCallersBeforeIgnoringProviderCompletes()
        {
            var deduplicator = new FeatureEntryDeduplicator<Handle>();
            var admitted = new TaskCompletionSource<Handle>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var scope = new CancellationTokenSource();
            int calls = 0;
            Task<FeatureEntryResolution<Handle>> request = deduplicator.ResolveAsync("tutorial:scope:event-1", _ => null,
                (_, _) => { calls++; return admitted.Task; }, scope.Token);
            scope.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await request);

            // Scope retirement releases the shared slot immediately, even while the old provider
            // is still running and ignoring its cancellation token.
            Task<FeatureEntryResolution<Handle>> retry = deduplicator.ResolveAsync("tutorial:scope:event-1", _ => null,
                (_, _) => { calls++; return Task.FromResult(new Handle()); }, CancellationToken.None);
            Assert.Equal(FeatureEntryResolutionKind.Admitted, (await retry).Kind);
            Assert.Equal(2, calls);

            admitted.SetResult(new Handle());
            Assert.Equal(2, calls);
        }

        [Fact]
        public async Task AdmissionDelegateRunsOutsideThePendingMapLockAndFailuresCanRetry()
        {
            var deduplicator = new FeatureEntryDeduplicator<Handle>();
            int calls = 0;
            Task<FeatureEntryResolution<Handle>> failed = deduplicator.ResolveAsync("entry:1", _ => null,
                (_, _) => { calls++; throw new InvalidOperationException("fixture"); }, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await failed);

            FeatureEntryResolution<Handle> retry = await deduplicator.ResolveAsync("entry:1", _ => null,
                (_, _) => { calls++; return Task.FromResult(new Handle()); }, CancellationToken.None);
            Assert.Equal(2, calls);
            Assert.Equal(FeatureEntryResolutionKind.Admitted, retry.Kind);
        }

        private sealed class Handle { }
    }
}
