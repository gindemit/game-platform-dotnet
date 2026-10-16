using System;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Navigation.Kernel.Flows
{
    /// <summary>
    /// Serializes checkpoint operations and publishes state only after a matching store acknowledgement.
    /// ScopeCurrent must reflect the owning account/event generation; Retire fences late stores that ignore cancellation.
    /// All awaits preserve the caller context. Cancellation settles observation promptly, but an ignored store
    /// keeps the operation gate until it settles, so a later write cannot overtake it. Late faults are observed.
    /// </summary>
    public sealed class TutorialFlow : IDisposable
    {
        private readonly ITutorialCheckpointStore _store;
        private readonly Func<bool> _scopeCurrent;
        private readonly CancellationTokenSource _lifetime;
        private readonly CancellationToken _lifetimeToken;
        private readonly SemaphoreSlim _operations = new SemaphoreSlim(1, 1);
        private readonly object _stateGate = new object();
        private TutorialCheckpoint? _checkpoint;
        private bool _retired;

        public TutorialFlow(TutorialCheckpointIdentity identity, ITutorialCheckpointStore store,
            Func<bool> scopeCurrent, CancellationToken scopeLifetime = default)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _scopeCurrent = scopeCurrent ?? throw new ArgumentNullException(nameof(scopeCurrent));
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(scopeLifetime);
            _lifetimeToken = _lifetime.Token;
        }

        public TutorialCheckpointIdentity Identity { get; }
        public TutorialCheckpoint? Checkpoint { get { lock (_stateGate) return _checkpoint; } }
        public bool IsCompleted => Checkpoint?.IsCompleted == true;

        /// <summary>Loads the last persisted safe point. No checkpoint means the tutorial may start at its first step.</summary>
        public Task<TutorialFlowResult> LoadAsync(CancellationToken cancellationToken = default) =>
            ObserveAsync(LoadCoreAsync(cancellationToken), cancellationToken);

        private async Task<TutorialFlowResult> LoadCoreAsync(CancellationToken cancellationToken)
        {
            if (!IsCurrent())
                return Result(TutorialFlowOutcome.Retired);
            using (CancellationTokenSource linked = Link(cancellationToken))
            {
                try
                { await _operations.WaitAsync(linked.Token); }
                catch (OperationCanceledException) { return Result(IsRetired() ? TutorialFlowOutcome.Retired : TutorialFlowOutcome.Cancelled); }
                try
                {
                    if (!IsCurrent())
                        return Result(TutorialFlowOutcome.Retired);
                    TutorialCheckpoint? loaded = await _store.LoadAsync(Identity, linked.Token);
                    if (linked.IsCancellationRequested)
                        return Result(IsRetired() ? TutorialFlowOutcome.Retired : TutorialFlowOutcome.Cancelled);
                    if (!IsCurrent())
                        return Result(TutorialFlowOutcome.Retired);
                    if (loaded != null && !Identity.Equals(loaded.Identity))
                        throw new InvalidOperationException("The checkpoint store returned a different tutorial identity.");
                    lock (_stateGate)
                    {
                        if (_retired || linked.IsCancellationRequested)
                            return new TutorialFlowResult(_retired || _lifetime.IsCancellationRequested
                                ? TutorialFlowOutcome.Retired : TutorialFlowOutcome.Cancelled, _checkpoint);
                        _checkpoint = loaded;
                    }
                    return new TutorialFlowResult(loaded == null ? TutorialFlowOutcome.NoCheckpoint : TutorialFlowOutcome.Acknowledged, loaded);
                }
                catch (OperationCanceledException) { return Result(IsRetired() ? TutorialFlowOutcome.Retired : TutorialFlowOutcome.Cancelled); }
                finally { _operations.Release(); }
            }
        }

        /// <summary>Persists a declared safe resume point; skipped tutorials remain incomplete.</summary>
        public Task<TutorialFlowResult> SaveCheckpointAsync(string stepKey, CancellationToken cancellationToken = default) =>
            SaveAsync(stepKey, TutorialCheckpointState.InProgress, cancellationToken);

        /// <summary>Completes only after the supplied final safe point is acknowledged by storage.</summary>
        public Task<TutorialFlowResult> CompleteAsync(string finalStepKey, CancellationToken cancellationToken = default) =>
            SaveAsync(finalStepKey, TutorialCheckpointState.Completed, cancellationToken);

        /// <summary>Records a dismissal without marking the tutorial complete.</summary>
        public Task<TutorialFlowResult> SkipAsync(string stepKey, CancellationToken cancellationToken = default) =>
            SaveAsync(stepKey, TutorialCheckpointState.Skipped, cancellationToken);

        public void Retire()
        {
            lock (_stateGate)
            {
                if (_retired)
                    return;
                _retired = true;
            }
            // Cancellation callbacks may run synchronously; never invoke them under the state lock.
            try
            { _lifetime.Cancel(); }
            catch (AggregateException) { /* A throwing observer cannot prevent this scope from staying retired. */ }
        }

        private Task<TutorialFlowResult> SaveAsync(string stepKey, TutorialCheckpointState state, CancellationToken cancellationToken) =>
            ObserveAsync(SaveCoreAsync(stepKey, state, cancellationToken), cancellationToken);

        private async Task<TutorialFlowResult> SaveCoreAsync(string stepKey, TutorialCheckpointState state, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(stepKey))
                throw new ArgumentException("A step key is required.", nameof(stepKey));
            if (!IsCurrent())
                return Result(TutorialFlowOutcome.Retired);
            using (CancellationTokenSource linked = Link(cancellationToken))
            {
                try
                { await _operations.WaitAsync(linked.Token); }
                catch (OperationCanceledException) { return Result(IsRetired() ? TutorialFlowOutcome.Retired : TutorialFlowOutcome.Cancelled); }
                try
                {
                    if (!IsCurrent())
                        return Result(TutorialFlowOutcome.Retired);
                    TutorialCheckpoint? before = Checkpoint;
                    if (before?.State == TutorialCheckpointState.Completed)
                        return Result(TutorialFlowOutcome.Rejected);
                    long revision = before == null ? 1 : checked(before.Revision + 1);
                    var candidate = new TutorialCheckpoint(Identity, stepKey, state, revision);
                    TutorialCheckpointSaveResult saved = await _store.SaveAsync(candidate, linked.Token);
                    if (linked.IsCancellationRequested)
                        return Result(IsRetired() ? TutorialFlowOutcome.Retired : TutorialFlowOutcome.Cancelled);
                    if (!IsCurrent())
                        return Result(TutorialFlowOutcome.Retired);
                    if (!saved.IsAcknowledged)
                        return Result(TutorialFlowOutcome.Rejected);
                    if (saved.Checkpoint == null || !candidate.Equals(saved.Checkpoint))
                        return Result(TutorialFlowOutcome.Stale);
                    lock (_stateGate)
                    {
                        if (_retired || linked.IsCancellationRequested)
                            return new TutorialFlowResult(_retired || _lifetime.IsCancellationRequested
                                ? TutorialFlowOutcome.Retired : TutorialFlowOutcome.Cancelled, _checkpoint);
                        _checkpoint = saved.Checkpoint;
                        return new TutorialFlowResult(TutorialFlowOutcome.Acknowledged, _checkpoint);
                    }
                }
                catch (OperationCanceledException) { return Result(IsRetired() ? TutorialFlowOutcome.Retired : TutorialFlowOutcome.Cancelled); }
                finally { _operations.Release(); }
            }
        }

        private async Task<TutorialFlowResult> ObserveAsync(Task<TutorialFlowResult> operation, CancellationToken caller)
        {
            using (CancellationTokenSource linked = Link(caller))
            {
                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (linked.Token.Register(() => cancelled.TrySetResult(true)))
                {
                    Task winner = await Task.WhenAny(operation, cancelled.Task);
                    if (!ReferenceEquals(winner, operation) || linked.IsCancellationRequested)
                    {
                        _ = ObserveLateOperationAsync(operation);
                        return Result(IsRetired() ? TutorialFlowOutcome.Retired : TutorialFlowOutcome.Cancelled);
                    }
                    return await operation;
                }
            }
        }

        private static async Task ObserveLateOperationAsync(Task operation)
        {
            try
            { await operation; }
            catch { /* The canceled observer cannot receive the late store failure. */ }
        }

        private CancellationTokenSource Link(CancellationToken caller) =>
            CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken, caller);
        private bool IsRetired()
        {
            lock (_stateGate)
                if (_retired)
                    return true;
            return _lifetime.IsCancellationRequested || !_scopeCurrent();
        }
        private bool IsCurrent() => !IsRetired();
        private TutorialFlowResult Result(TutorialFlowOutcome outcome) => new TutorialFlowResult(outcome, Checkpoint);

        public void Dispose()
        {
            Retire();
            _lifetime.Dispose();
            // _operations is intentionally retained: a store may still be unwinding after ignored cancellation.
        }
    }
}
