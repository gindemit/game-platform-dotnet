using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Navigation.Kernel
{
    /// <summary>Owns logical history. Admission, observers and cancellation callbacks always run outside its state lock.</summary>
    public sealed class NavigationKernel : IDisposable
    {
        private static long entrySequence;
        private static long flowSequence;
        private readonly object gate = new object();
        private readonly string scopeId;
        private readonly int capacity;
        private readonly bool legacy;
        private readonly List<Record> entries = new List<Record>();
        private readonly List<Record> modals = new List<Record>();
        private readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>(StringComparer.Ordinal);
        private long generation;
        private long version;
        private long operationSequence;
        private bool disposed;

        public NavigationKernel(string scopeId, long generation = 0, int capacity = 16)
            : this(scopeId, generation, capacity, false) { }

        internal NavigationKernel(string scopeId, long generation, int capacity, bool legacy)
        {
            if (string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("Scope is required.", nameof(scopeId));
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            if (capacity < 1 || capacity > 1024)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            this.scopeId = scopeId;
            this.generation = generation;
            this.capacity = capacity;
            this.legacy = legacy;
        }

        public event Action<NavigationSnapshot>? SnapshotChanged;
        public NavigationSnapshot Snapshot { get { lock (gate) return Capture(); } }

        public Task<NavigationOpenResult<TResult>> SelectRootAsync<TArgs, TResult>(NavigationRequest<TArgs, TResult> request,
            NavigationAdmission? admission = null, CancellationToken cancellationToken = default) =>
            OpenAsync(request, Operation.Root, null, admission, cancellationToken);

        public Task<NavigationOpenResult<TResult>> PushAsync<TArgs, TResult>(NavigationRequest<TArgs, TResult> request,
            NavigationAdmission? admission = null, CancellationToken cancellationToken = default) =>
            OpenAsync(request, Operation.Push, null, admission, cancellationToken);

        public Task<NavigationOpenResult<TResult>> ReplaceAsync<TArgs, TResult>(NavigationRequest<TArgs, TResult> request,
            NavigationAdmission? admission = null, CancellationToken cancellationToken = default) =>
            OpenAsync(request, Operation.Replace, null, admission, cancellationToken);

        public Task<NavigationOpenResult<TResult>> OpenModalAsync<TArgs, TResult>(EntryId owner, NavigationRequest<TArgs, TResult> request,
            NavigationAdmission? admission = null, CancellationToken cancellationToken = default) =>
            OpenAsync(request, Operation.Modal, owner, admission, cancellationToken);

        private async Task<NavigationOpenResult<TResult>> OpenAsync<TArgs, TResult>(NavigationRequest<TArgs, TResult> request,
            Operation operation, EntryId? owner, NavigationAdmission? admission, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            Pending? opening = null;
            Pending? joined = null;
            NavigationOpenResult<TResult>? immediate = null;
            var effects = new List<Action>();
            lock (gate)
            {
                if (disposed)
                    immediate = Rejected<TResult>(NavigationOutcome.Retired);
                else if (!legacy && cancellationToken.IsCancellationRequested)
                    immediate = Rejected<TResult>(NavigationOutcome.Cancelled);
                else if ((operation == Operation.Modal) != (request.Definition.Presentation == NavigationPresentation.Modal))
                    immediate = Rejected<TResult>(NavigationOutcome.InvalidRequest);
                else
                {
                    var anchor = operation == Operation.Modal ? Find(owner!.Value) : entries.LastOrDefault();
                    if (operation != Operation.Root && anchor == null)
                        immediate = Rejected<TResult>(NavigationOutcome.InvalidRequest);
                    else
                    {
                        var reused = FindReusable(request);
                        joined = operation == Operation.Root ? null : pending.Values.FirstOrDefault(x =>
                            x.Operation == operation && x.Anchor == anchor?.Entry.Id && Matches(x.Record, request));
                        if (joined != null)
                        { /* Await its shell admission outside the lock. */ }
                        else if (reused != null && operation != Operation.Root)
                        {
                            if (reused.Promise is Promise<TResult> promise)
                            {
                                // Reuse focuses the existing logical entry without manufacturing a new caller/result owner.
                                CloseDescendants(reused, effects);
                                immediate = new NavigationOpenResult<TResult>(NavigationOutcome.Reused, reused.Entry, promise.Task);
                                Changed(effects);
                            }
                            else
                                immediate = Rejected<TResult>(NavigationOutcome.InvalidRequest);
                        }
                        else if (operation == Operation.Modal && !legacy && Top()?.Entry.Id != owner)
                            immediate = Rejected<TResult>(NavigationOutcome.InvalidRequest);
                        else if (operation == Operation.Push && !legacy && entries.Count + modals.Count >= capacity)
                            immediate = Rejected<TResult>(NavigationOutcome.CapacityExceeded);
                        else if (operation == Operation.Modal && !legacy && entries.Count + modals.Count >= capacity)
                            immediate = Rejected<TResult>(NavigationOutcome.CapacityExceeded);
                        else
                        {
                            var slot = legacy ? (operation == Operation.Modal ? "legacy-modal" : "legacy") : operation == Operation.Root ? "root" :
                                (operation == Operation.Modal ? "modal:" : "screen:") + anchor!.Entry.Id;
                            if (pending.TryGetValue(slot, out var prior))
                                StopPending(prior, NavigationOutcome.Stale, effects);
                            var lifetime = new CancellationTokenSource();
                            var result = new Promise<TResult>();
                            var parent = operation == Operation.Root ? null : operation == Operation.Replace ? anchor!.Entry.ParentId : anchor!.Entry.Id;
                            var flow = operation == Operation.Root ? NextFlow() : anchor!.Entry.FlowId;
                            var entry = new NavigationEntry(NextEntry(), flow, scopeId, generation, parent,
                                request.Definition.Key, request.Arguments, typeof(TArgs), typeof(TResult), request.Definition.Presentation,
                                request.MatchKey, lifetime.Token);
                            var record = new Record(entry, lifetime, result);
                            opening = new Pending(++operationSequence, slot, operation, anchor?.Entry.Id, record, cancellationToken);
                            pending.Add(slot, opening);
                            Changed(effects);
                        }
                    }
                }
            }
            Dispatch(effects);
            if (immediate != null)
                return immediate;
            if (joined != null)
                return await JoinAsync<TResult>(joined, cancellationToken).ConfigureAwait(false);
            var active = opening!;
            var admitted = NavigationAdmissionOutcome.Available;
            var failure = NavigationOutcome.Applied;
            try
            {
                if (admission != null)
                {
                    var work = admission(active.Record.Entry, active.Request.Token);
                    if (work == null)
                        throw new InvalidOperationException("Admission returned no task.");
                    if (legacy)
                        admitted = await work.ConfigureAwait(false);
                    else
                    {
                        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        using (active.Request.Token.Register(() => cancelled.TrySetResult(true)))
                        {
                            if (await Task.WhenAny(work, cancelled.Task).ConfigureAwait(false) != work)
                            {
                                Observe(work);
                                throw new OperationCanceledException(active.Request.Token);
                            }
                            admitted = await work.ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { failure = NavigationOutcome.Cancelled; }
            catch { failure = NavigationOutcome.Failed; }

            effects = new List<Action>();
            NavigationOutcome outcome;
            lock (gate)
            {
                if (active.EndReason.HasValue)
                    outcome = active.EndReason.Value;
                else if (disposed || active.Record.Entry.Generation != generation)
                    outcome = NavigationOutcome.Retired;
                else if (cancellationToken.IsCancellationRequested)
                    outcome = NavigationOutcome.Cancelled;
                else if (failure != NavigationOutcome.Applied)
                    outcome = failure;
                else if (admitted != NavigationAdmissionOutcome.Available)
                    outcome = NavigationOutcome.Unavailable;
                else if (!CanCommit(active))
                    outcome = NavigationOutcome.Stale;
                else if (!legacy && (operation == Operation.Push || operation == Operation.Modal) && entries.Count + modals.Count >= capacity)
                    outcome = NavigationOutcome.CapacityExceeded;
                else
                    outcome = NavigationOutcome.Applied;

                if (!active.EndReason.HasValue)
                {
                    pending.Remove(active.Slot);
                    if (outcome == NavigationOutcome.Applied)
                        Commit(active, effects);
                    else
                        Retire(active.Record, NavigationChildStatus.Cancelled, effects);
                    active.EndReason = outcome;
                    effects.Add(() => active.Completion.TrySetResult(outcome));
                    Changed(effects);
                }
            }
            Dispatch(effects);
            active.Request.Dispose();
            return new NavigationOpenResult<TResult>(outcome, outcome == NavigationOutcome.Applied ? active.Record.Entry : null,
                ((Promise<TResult>)active.Record.Promise).Task);
        }

        public NavigationOutcome Back()
        {
            var effects = new List<Action>();
            NavigationOutcome outcome;
            lock (gate)
            {
                if (disposed)
                    return NavigationOutcome.Retired;
                if (modals.Count > 0)
                { CloseRecord(modals[modals.Count - 1], effects); outcome = NavigationOutcome.Applied; }
                else if (pending.Count > 0)
                { StopPending(pending.Values.OrderByDescending(x => x.Sequence).First(), NavigationOutcome.Cancelled, effects); outcome = NavigationOutcome.Applied; }
                else if (entries.Count > 1)
                { CloseRecord(entries[entries.Count - 1], effects); outcome = NavigationOutcome.Applied; }
                else
                    return NavigationOutcome.NoChange;
                Changed(effects);
            }
            Dispatch(effects);
            return outcome;
        }

        public NavigationOutcome Close(EntryId entryId)
        {
            var effects = new List<Action>();
            lock (gate)
            {
                if (disposed)
                    return NavigationOutcome.Retired;
                var record = Find(entryId);
                if (record == null)
                {
                    var provisional = pending.Values.FirstOrDefault(x => x.Record.Entry.Id == entryId);
                    if (provisional == null)
                        return NavigationOutcome.NoChange;
                    StopPending(provisional, NavigationOutcome.Cancelled, effects);
                }
                else
                    CloseRecord(record, effects);
                Changed(effects);
            }
            Dispatch(effects);
            return NavigationOutcome.Applied;
        }

        public NavigationOutcome Complete<TResult>(EntryId entryId, TResult value)
        {
            var effects = new List<Action>();
            lock (gate)
            {
                if (disposed)
                    return NavigationOutcome.Retired;
                var record = Find(entryId);
                if (record == null)
                    return NavigationOutcome.NoChange;
                if (!(record.Promise is Promise<TResult> result))
                    return NavigationOutcome.InvalidRequest;
                if (!record.Entry.ParentId.HasValue || Find(record.Entry.ParentId.Value) == null)
                    return NavigationOutcome.InvalidRequest;
                CloseDescendants(record, effects);
                entries.Remove(record);
                modals.Remove(record);
                record.Retired = true;
                effects.Add(() => DeliverResult(record.Entry, result, value));
                effects.Add(() => CancelAndDispose(record.Lifetime));
                Changed(effects);
            }
            Dispatch(effects);
            return NavigationOutcome.Applied;
        }

        public NavigationOutcome SaveRestorationState<T>(EntryId entryId, T state)
        {
            var effects = new List<Action>();
            lock (gate)
            {
                if (disposed)
                    return NavigationOutcome.Retired;
                var record = Find(entryId);
                if (record == null)
                    return NavigationOutcome.NoChange;
                record.Restoration = state;
                Changed(effects);
            }
            Dispatch(effects);
            return NavigationOutcome.Applied;
        }

        public NavigationOutcome ReturnToRoot()
        {
            var effects = new List<Action>();
            lock (gate)
            {
                if (disposed)
                    return NavigationOutcome.Retired;
                if (entries.Count <= 1 && modals.Count == 0 && pending.Count == 0)
                    return NavigationOutcome.NoChange;
                foreach (var opening in pending.Values.ToArray())
                    StopPending(opening, NavigationOutcome.Cancelled, effects);
                if (entries.Count > 0)
                    CloseDescendants(entries[0], effects);
                foreach (var modal in modals.ToArray().Reverse())
                    CloseRecord(modal, effects);
                Changed(effects);
            }
            Dispatch(effects);
            return NavigationOutcome.Applied;
        }

        public NavigationOutcome RetireScope(long nextGeneration)
        {
            var effects = new List<Action>();
            lock (gate)
            {
                if (disposed)
                    return NavigationOutcome.Retired;
                if (nextGeneration <= generation)
                    throw new ArgumentOutOfRangeException(nameof(nextGeneration));
                generation = nextGeneration;
                RetireAll(effects);
                Changed(effects);
            }
            Dispatch(effects);
            return NavigationOutcome.Applied;
        }

        public void Dispose()
        {
            var effects = new List<Action>();
            lock (gate)
            {
                if (disposed)
                    return;
                disposed = true;
                RetireAll(effects);
                Changed(effects);
            }
            Dispatch(effects);
        }

        private void Commit(Pending opening, List<Action> effects)
        {
            switch (opening.Operation)
            {
                case Operation.Root:
                    RetireAll(effects);
                    entries.Add(opening.Record);
                    break;
                case Operation.Push:
                    if (legacy && entries.Count == capacity + 1)
                    {
                        var evicted = entries[0];
                        entries.RemoveAt(0);
                        Retire(evicted, NavigationChildStatus.Cancelled, effects);
                    }
                    entries.Add(opening.Record);
                    break;
                case Operation.Replace:
                    var previous = entries[entries.Count - 1];
                    if (legacy)
                    { entries.Remove(previous); Retire(previous, NavigationChildStatus.Cancelled, effects); }
                    else
                        CloseRecord(previous, effects);
                    entries.Add(opening.Record);
                    break;
                case Operation.Modal:
                    if (legacy)
                        foreach (var modal in modals.ToArray().Reverse())
                            CloseRecord(modal, effects);
                    modals.Add(opening.Record);
                    break;
            }
        }

        private bool CanCommit(Pending opening)
        {
            if (!pending.TryGetValue(opening.Slot, out var current) || !ReferenceEquals(current, opening))
                return false;
            if (opening.Operation == Operation.Root)
                return true;
            var anchor = opening.Anchor.HasValue ? Find(opening.Anchor.Value) : null;
            if (anchor == null)
                return false;
            if (opening.Operation == Operation.Modal)
                return legacy || Top() == anchor;
            return entries.LastOrDefault() == anchor;
        }

        private Record? FindReusable<TArgs, TResult>(NavigationRequest<TArgs, TResult> request)
        {
            return entries.Concat(modals).FirstOrDefault(x => Matches(x, request));
        }

        private static bool Matches<TArgs, TResult>(Record record, NavigationRequest<TArgs, TResult> request) =>
            request.Definition.Multiplicity != NavigationMultiplicity.Multiple &&
            record.Entry.DefinitionKey == request.Definition.Key && record.Entry.ArgumentsType == typeof(TArgs) &&
            record.Entry.ResultType == typeof(TResult) && record.Entry.Presentation == request.Definition.Presentation &&
            (request.Definition.Multiplicity == NavigationMultiplicity.SingletonPerScope || record.Entry.MatchKey == request.MatchKey);

        private void DeliverResult<TResult>(NavigationEntry entry, Promise<TResult> result, TResult value)
        {
            bool ownerAlive;
            lock (gate)
                ownerAlive = !disposed && entry.Generation == generation && entry.ParentId.HasValue && Find(entry.ParentId.Value) != null;
            // Descendant cancellation callbacks may have retired the caller since Complete's state mutation.
            if (ownerAlive)
                result.Complete(value);
            else
                result.Cancel(NavigationChildStatus.OwnerRetired);
        }

        private static async Task<NavigationOpenResult<TResult>> JoinAsync<TResult>(Pending opening, CancellationToken token)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(opening.Completion.Task, cancelled.Task).ConfigureAwait(false) != opening.Completion.Task || token.IsCancellationRequested)
                    return Rejected<TResult>(NavigationOutcome.Cancelled);
                var outcome = await opening.Completion.Task.ConfigureAwait(false);
                return new NavigationOpenResult<TResult>(outcome == NavigationOutcome.Applied ? NavigationOutcome.Reused : outcome,
                    outcome == NavigationOutcome.Applied ? opening.Record.Entry : null, ((Promise<TResult>)opening.Record.Promise).Task);
            }
        }

        private void CloseRecord(Record record, List<Action> effects)
        {
            if (record.Retired)
                return;
            CloseDescendants(record, effects);
            entries.Remove(record);
            modals.Remove(record);
            Retire(record, NavigationChildStatus.Cancelled, effects);
        }

        private void CloseDescendants(Record record, List<Action> effects)
        {
            // Determine the entire ownership set before removing ancestors, then retire in reverse creation order.
            var owned = new HashSet<EntryId> { record.Entry.Id };
            foreach (var candidate in entries.Concat(modals).OrderBy(x => x.Entry.Id.Value))
                if (candidate.Entry.ParentId.HasValue && owned.Contains(candidate.Entry.ParentId.Value))
                    owned.Add(candidate.Entry.Id);
            foreach (var opening in pending.Values.ToArray())
                if ((opening.Anchor.HasValue && owned.Contains(opening.Anchor.Value)) ||
                    (opening.Record.Entry.ParentId.HasValue && owned.Contains(opening.Record.Entry.ParentId.Value)))
                    StopPending(opening, NavigationOutcome.Stale, effects);
            foreach (var candidate in entries.Concat(modals).Where(x => x != record && owned.Contains(x.Entry.Id)).OrderByDescending(x => x.Entry.Id.Value).ToArray())
            {
                entries.Remove(candidate);
                modals.Remove(candidate);
                Retire(candidate, NavigationChildStatus.OwnerRetired, effects);
            }
        }

        private void RetireAll(List<Action> effects)
        {
            foreach (var opening in pending.Values.ToArray())
                StopPending(opening, NavigationOutcome.Retired, effects);
            foreach (var record in entries.Concat(modals).OrderByDescending(x => x.Entry.Id.Value))
                Retire(record, NavigationChildStatus.OwnerRetired, effects);
            entries.Clear();
            modals.Clear();
        }

        private void StopPending(Pending opening, NavigationOutcome reason, List<Action> effects)
        {
            if (opening.EndReason.HasValue)
                return;
            opening.EndReason = reason;
            pending.Remove(opening.Slot);
            effects.Add(() => Cancel(opening.Request));
            Retire(opening.Record, reason == NavigationOutcome.Retired ? NavigationChildStatus.OwnerRetired : NavigationChildStatus.Cancelled, effects);
            effects.Add(() => opening.Completion.TrySetResult(reason));
        }

        private static void Retire(Record record, NavigationChildStatus status, List<Action> effects)
        {
            if (record.Retired)
                return;
            record.Retired = true;
            effects.Add(() => CancelAndDispose(record.Lifetime));
            effects.Add(() => record.Promise.Cancel(status));
        }

        private Record? Find(EntryId id) => entries.Concat(modals).FirstOrDefault(x => x.Entry.Id == id);
        private Record? Top() => modals.LastOrDefault() ?? entries.LastOrDefault();

        private NavigationSnapshot Capture()
        {
            var top = Top();
            NavigationEntrySnapshot SnapshotOf(Record record) => new NavigationEntrySnapshot(record.Entry,
                record == top ? NavigationEntryState.Active : NavigationEntryState.Covered, record.Restoration);
            return new NavigationSnapshot(version, scopeId, generation, entries.Select(SnapshotOf), modals.Select(SnapshotOf),
                pending.Values.OrderBy(x => x.Sequence).Select(x => new NavigationEntrySnapshot(x.Record.Entry, NavigationEntryState.Provisional, null)));
        }

        private void Changed(List<Action> effects)
        {
            version = checked(version + 1);
            var snapshot = Capture();
            var observers = SnapshotChanged;
            if (observers != null)
                foreach (Action<NavigationSnapshot> observer in observers.GetInvocationList())
                    effects.Add(() => observer(snapshot));
        }

        private static void Dispatch(List<Action> effects)
        {
            foreach (var effect in effects)
            { try { effect(); } catch { /* One observer cannot prevent mandatory cleanup. */ } }
        }
        private static void Cancel(CancellationTokenSource source) { try { source.Cancel(); } catch (AggregateException) { } catch (ObjectDisposedException) { } }
        private static void CancelAndDispose(CancellationTokenSource source) { Cancel(source); source.Dispose(); }
        private static void Observe(Task work) => _ = work.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        private static EntryId NextEntry() => new EntryId(Interlocked.Increment(ref entrySequence));
        private static FlowId NextFlow() => new FlowId(Interlocked.Increment(ref flowSequence));
        private static NavigationOpenResult<TResult> Rejected<TResult>(NavigationOutcome outcome) =>
            new NavigationOpenResult<TResult>(outcome, null, Task.FromResult(new NavigationChildResult<TResult>(NavigationChildStatus.Cancelled, default!)));

        private enum Operation { Root, Push, Replace, Modal }
        private sealed class Record
        {
            public Record(NavigationEntry entry, CancellationTokenSource lifetime, IResultPromise promise)
            { Entry = entry; Lifetime = lifetime; Promise = promise; }
            public NavigationEntry Entry { get; }
            public CancellationTokenSource Lifetime { get; }
            public IResultPromise Promise { get; }
            public object? Restoration { get; set; }
            public bool Retired { get; set; }
        }
        private sealed class Pending
        {
            public Pending(long sequence, string slot, Operation operation, EntryId? anchor, Record record, CancellationToken caller)
            {
                Sequence = sequence;
                Slot = slot;
                Operation = operation;
                Anchor = anchor;
                Record = record;
                Request = CancellationTokenSource.CreateLinkedTokenSource(caller);
            }
            public long Sequence { get; }
            public string Slot { get; }
            public Operation Operation { get; }
            public EntryId? Anchor { get; }
            public Record Record { get; }
            public CancellationTokenSource Request { get; }
            public NavigationOutcome? EndReason { get; set; }
            public TaskCompletionSource<NavigationOutcome> Completion { get; } = new TaskCompletionSource<NavigationOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        private interface IResultPromise { void Cancel(NavigationChildStatus status); }
        private sealed class Promise<TResult> : IResultPromise
        {
            private readonly TaskCompletionSource<NavigationChildResult<TResult>> source = new TaskCompletionSource<NavigationChildResult<TResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<NavigationChildResult<TResult>> Task => source.Task;
            public void Complete(TResult value) => source.TrySetResult(new NavigationChildResult<TResult>(NavigationChildStatus.Completed, value));
            public void Cancel(NavigationChildStatus status) => source.TrySetResult(new NavigationChildResult<TResult>(status, default!));
        }
    }
}
