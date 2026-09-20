#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Features.Progression
{
    /// <summary>Portable pending-completion coordinator. It is deliberately not a game validator, checkpoint serializer, or value allocator.</summary>
    public sealed class ProgressionService : IDisposable
    {
        private const string Namespace = "progression";
        private const string ConfirmedKey = "confirmed";
        private const string PendingKey = "pending";
        private readonly ScopedOwnerContext owner;
        private readonly StorageScope scope;
        private readonly IDurableFeatureStateStore state;
        private readonly ISerializedStorageExecutor transactions;
        private readonly IAtomicCommandStore commands;
        private readonly IProgressionStateCodec codec;
        private readonly Func<long> nowMilliseconds;
        private readonly SemaphoreSlim mutation = new SemaphoreSlim(1, 1);
        private bool disposed;

        public ProgressionService(ScopedOwnerContext owner, StorageScope scope, IDurableFeatureStateStore state,
            ISerializedStorageExecutor transactions, IAtomicCommandStore commands, IProgressionStateCodec codec, Func<long> nowMilliseconds)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!Matches(scope, owner.Owner)) throw new ArgumentException("The progression scope does not match the captured owner.", nameof(scope));
            this.owner = owner; this.scope = scope; this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions)); this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec)); this.nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
        }

        public Task<FeatureSnapshot<ProgressionSnapshot>> ReadCachedAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken) =>
            ReadSnapshotAsync(EnsureOwner(requestedOwner), SnapshotFreshness.Stale, cancellationToken);

        /// <summary>Commits the game supplied outcome and immutable command atomically. No local rule check, checkpoint write, progress increment, or reward occurs here.</summary>
        public async Task<FeatureSnapshot<ProgressionSnapshot>> CompleteAsync(ProgressionCompletionRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var exactOwner = EnsureOwner(request.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                var sameOperation = current.Pending.FirstOrDefault(value => value.OperationId == request.OperationId);
                var sameSource = current.Pending.FirstOrDefault(value => string.Equals(value.BusinessSource, request.BusinessSource, StringComparison.Ordinal));
                if (sameOperation != null || sameSource != null)
                {
                    var existing = sameOperation ?? sameSource!;
                    if (Equivalent(existing, request)) return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
                    throw new ProgressionConflictException("The operation or business source was already retained with different immutable completion semantics.");
                }
                var draftPending = new PendingProgressionCompletion(request.OperationId, request.StreamId, request.BusinessSource, request.Outcome,
                    request.ContentVersion, request.OutcomeAuthority, 1, ProgressionPendingStatus.AwaitingReceipt);
                var body = codec.EncodeCompletionCommand(draftPending); ValidatePayload(body, "completion command");
                var draft = new CommandDraft(exactOwner.Owner, request.OperationId, request.StreamId, "gameplay.session.completed", 1, 1, body);
                await commands.CommitAsync(request.BusinessSource, draft, (transaction, localRevision) =>
                {
                    var pending = new PendingProgressionCompletion(request.OperationId, request.StreamId, request.BusinessSource, request.Outcome,
                        request.ContentVersion, request.OutcomeAuthority, localRevision, ProgressionPendingStatus.AwaitingReceipt);
                    var all = current.Pending.Concat(new[] { pending }).ToArray();
                    var encoded = codec.EncodePending(all); ValidatePayload(encoded, "pending completions");
                    state.Upsert(transaction, new DurableFeatureMutation(exactOwner, Namespace, PendingKey, localRevision, Now(), encoded, Array.Empty<byte>()));
                }, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Records a retained terminal acceptance while retaining the completion until a matching pull-derived confirmation.</summary>
        public async Task<FeatureSnapshot<ProgressionSnapshot>> MarkAcceptedAwaitingPullAsync(ProgressionAcceptance acceptance, CancellationToken cancellationToken)
        {
            if (acceptance == null) throw new ArgumentNullException(nameof(acceptance));
            var exactOwner = EnsureOwner(acceptance.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                var existing = current.Pending.FirstOrDefault(value => value.OperationId == acceptance.OperationId);
                if (existing == null) throw new ProgressionConflictException("The accepted receipt does not match a pending completion.");
                if (existing.Status == ProgressionPendingStatus.AwaitingReceipt)
                    await WritePendingAsync(exactOwner, current.Pending.Select(value => value.OperationId == acceptance.OperationId
                        ? new PendingProgressionCompletion(value.OperationId, value.StreamId, value.BusinessSource, value.Outcome, value.ContentVersion,
                            value.OutcomeAuthority, value.LocalRevision, ProgressionPendingStatus.AcceptedAwaitingPull) : value).ToArray(), current.PendingRevision, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Applies an explicit pull-derived acknowledgement and authoritative projection, preserving unrelated pending completions.</summary>
        public async Task ApplyConfirmedAsync(ProgressionConfirmation confirmation, CancellationToken cancellationToken)
        {
            if (confirmation == null) throw new ArgumentNullException(nameof(confirmation));
            var exactOwner = EnsureOwner(confirmation.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                if (current.Confirmed != null && confirmation.Projection.Revision < current.Confirmed.Revision) return;
                if (current.Confirmed != null && confirmation.Projection.Revision == current.Confirmed.Revision && !Equivalent(current.Confirmed, confirmation.Projection))
                    throw new ProgressionConflictException("The server supplied different progression contents for the same revision.");
                var confirmedIds = new HashSet<OperationId>(confirmation.ConfirmedOperationIds);
                var retained = current.Pending.Where(value => !(value.Status == ProgressionPendingStatus.AcceptedAwaitingPull && confirmedIds.Contains(value.OperationId))).ToArray();
                await transactions.ExecuteAsync(scope, transaction =>
                {
                    if (current.Confirmed == null || confirmation.Projection.Revision > current.Confirmed.Revision)
                    {
                        var encodedConfirmed = codec.EncodeConfirmed(confirmation.Projection); ValidatePayload(encodedConfirmed, "confirmed progression");
                        state.Upsert(transaction, new DurableFeatureMutation(exactOwner, Namespace, ConfirmedKey, confirmation.Projection.Revision,
                            confirmation.Projection.ConfirmedAtMilliseconds, encodedConfirmed, Array.Empty<byte>()));
                    }
                    if (retained.Length != current.Pending.Count)
                    {
                        var pendingRevision = retained.Length == 0 ? Math.Max(1, current.PendingRevision) : Math.Max(current.PendingRevision, retained.Max(value => value.LocalRevision));
                        var encodedPending = retained.Length == 0 ? Array.Empty<byte>() : codec.EncodePending(retained);
                        if (encodedPending.Length != 0) ValidatePayload(encodedPending, "pending completions");
                        state.Upsert(transaction, new DurableFeatureMutation(exactOwner, Namespace, PendingKey, pendingRevision, Now(), encodedPending, Array.Empty<byte>()));
                    }
                    return true;
                }, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
        }

        /// <summary>Receipts may terminally reject only their matching local completion. They never alter confirmed progression or other pending work.</summary>
        public async Task<FeatureSnapshot<ProgressionSnapshot>> RejectAsync(ScopedOwnerContext requestedOwner, OperationId operationId, CancellationToken cancellationToken)
        {
            var exactOwner = EnsureOwner(requestedOwner);
            if (!operationId.IsValid) throw new ArgumentException("A valid operation ID is required.", nameof(operationId));
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                var retained = current.Pending.Where(value => value.OperationId != operationId).ToArray();
                if (retained.Length == current.Pending.Count) throw new ProgressionConflictException("The rejected receipt does not match a pending completion.");
                await WritePendingAsync(exactOwner, retained, current.PendingRevision, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose() { if (disposed) return; disposed = true; mutation.Dispose(); }

        private async Task<FeatureSnapshot<ProgressionSnapshot>> ReadSnapshotAsync(ScopedOwnerContext exactOwner, SnapshotFreshness freshness, CancellationToken cancellationToken)
        {
            var durable = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
            if (durable.Confirmed == null && durable.Pending.Count == 0)
                return new FeatureSnapshot<ProgressionSnapshot>(exactOwner, 0, FeatureSnapshotState.Missing, SnapshotFreshness.Missing, null, null, null);
            var value = new ProgressionSnapshot(durable.Confirmed, durable.Pending);
            var revision = durable.Confirmed?.Revision ?? 0;
            if (durable.Pending.Count != 0)
                return new FeatureSnapshot<ProgressionSnapshot>(exactOwner, revision, FeatureSnapshotState.Pending, freshness, value, durable.Confirmed?.ConfirmedAtMilliseconds, null);
            return new FeatureSnapshot<ProgressionSnapshot>(exactOwner, revision, freshness == SnapshotFreshness.Current ? FeatureSnapshotState.Available : FeatureSnapshotState.Stale,
                freshness, value, durable.Confirmed!.ConfirmedAtMilliseconds, null);
        }

        private async Task<(ProgressionConfirmedProjection? Confirmed, IReadOnlyList<PendingProgressionCompletion> Pending, long PendingRevision)> ReadDurableAsync(ScopedOwnerContext exactOwner, CancellationToken cancellationToken)
        {
            var confirmed = await state.ReadAsync(exactOwner, Namespace, ConfirmedKey, cancellationToken).ConfigureAwait(false);
            var pending = await state.ReadAsync(exactOwner, Namespace, PendingKey, cancellationToken).ConfigureAwait(false);
            ProgressionConfirmedProjection? decodedConfirmed = null;
            if (confirmed != null)
            {
                decodedConfirmed = codec.DecodeConfirmed(confirmed.Revision, confirmed.ConfirmedAtMilliseconds, confirmed.CopyPayload());
                if (decodedConfirmed == null || decodedConfirmed.Revision != confirmed.Revision || decodedConfirmed.ConfirmedAtMilliseconds != confirmed.ConfirmedAtMilliseconds)
                    throw new ProgressionConflictException("The progression codec returned an invalid confirmed projection.");
            }
            if (pending == null || pending.PayloadLength == 0) return (decodedConfirmed, Array.Empty<PendingProgressionCompletion>(), pending?.Revision ?? 0);
            var decodedPending = codec.DecodePending(pending.Revision, pending.CopyPayload());
            if (decodedPending == null || decodedPending.Any(value => value == null) || decodedPending.Any(value => value.LocalRevision > pending.Revision) ||
                decodedPending.GroupBy(value => value.OperationId).Any(group => group.Count() != 1) ||
                decodedPending.GroupBy(value => value.BusinessSource, StringComparer.Ordinal).Any(group => group.Count() != 1))
                throw new ProgressionConflictException("The progression codec returned invalid pending completions.");
            return (decodedConfirmed, decodedPending.ToArray(), pending.Revision);
        }

        private Task WritePendingAsync(ScopedOwnerContext exactOwner, IReadOnlyList<PendingProgressionCompletion> pending, long currentRevision, CancellationToken cancellationToken) =>
            transactions.ExecuteAsync(scope, transaction =>
            {
                var revision = Math.Max(1, Math.Max(currentRevision, pending.Count == 0 ? 0 : pending.Max(value => value.LocalRevision)));
                var payload = pending.Count == 0 ? Array.Empty<byte>() : codec.EncodePending(pending);
                if (payload.Length != 0) ValidatePayload(payload, "pending completions");
                state.Upsert(transaction, new DurableFeatureMutation(exactOwner, Namespace, PendingKey, revision, Now(), payload, Array.Empty<byte>()));
                return true;
            }, cancellationToken);

        private ScopedOwnerContext EnsureOwner(ScopedOwnerContext requestedOwner) { if (disposed) throw new ObjectDisposedException(nameof(ProgressionService)); if (requestedOwner != owner) throw new ProgressionOwnerMismatchException(); return requestedOwner; }
        private long Now() { var value = nowMilliseconds(); ProgressionValidation.Timestamp(value, nameof(nowMilliseconds)); return value; }
        private static bool Matches(StorageScope candidate, OwnerScope value) => string.Equals(candidate.BackendNamespace, value.Backend.Value, StringComparison.Ordinal) && string.Equals(candidate.AppId.Value, value.AppId.ToString(), StringComparison.Ordinal) && string.Equals(candidate.AccountId.Value, value.UserId.ToString(), StringComparison.Ordinal);
        private static void ValidatePayload(byte[]? value, string label) { if (value == null || value.Length == 0 || value.Length > 262_144) throw new InvalidOperationException("The " + label + " codec payload is invalid."); }
        private static bool Equivalent(PendingProgressionCompletion existing, ProgressionCompletionRequest request) => existing.StreamId == request.StreamId && string.Equals(existing.BusinessSource, request.BusinessSource, StringComparison.Ordinal) && existing.ContentVersion == request.ContentVersion && existing.OutcomeAuthority == request.OutcomeAuthority && Equivalent(existing.Outcome, request.Outcome);
        private static bool Equivalent(GameplayOutcome left, GameplayOutcome right) => left.Session.Equals(right.Session) && left.Mode.Equals(right.Mode) && left.Content.Equals(right.Content) && left.Difficulty.Equals(right.Difficulty) && left.Success == right.Success && left.Score == right.Score && left.DurationTicks == right.DurationTicks && left.TicksPerSecond == right.TicksPerSecond && string.Equals(left.ValidationReference, right.ValidationReference, StringComparison.Ordinal) && left.Metrics.Count == right.Metrics.Count && left.Metrics.All(value => right.Metrics.TryGetValue(value.Key, out var matched) && matched == value.Value);
        private static bool Equivalent(ProgressionConfirmedProjection left, ProgressionConfirmedProjection right) => left.Revision == right.Revision && left.ConfirmedAtMilliseconds == right.ConfirmedAtMilliseconds && left.States.Count == right.States.Count && left.States.All(value => right.States.Any(other => other.StateKey == value.StateKey && other.Value == value.Value));
    }
}
