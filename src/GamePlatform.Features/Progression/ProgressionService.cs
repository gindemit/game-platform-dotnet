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
        private readonly IProgressionConfirmationEvidenceStore confirmations;
        private readonly Func<long> nowMilliseconds;
        private readonly SemaphoreSlim mutation = new SemaphoreSlim(1, 1);
        private bool disposed;

        public ProgressionService(ScopedOwnerContext owner, StorageScope scope, IDurableFeatureStateStore state,
            ISerializedStorageExecutor transactions, IAtomicCommandStore commands, IProgressionStateCodec codec,
            IProgressionConfirmationEvidenceStore confirmations, Func<long> nowMilliseconds)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!Matches(scope, owner.Owner)) throw new ArgumentException("The progression scope does not match the captured owner.", nameof(scope));
            this.owner = owner; this.scope = scope; this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions)); this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec)); this.confirmations = confirmations ?? throw new ArgumentNullException(nameof(confirmations));
            this.nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
        }

        public Task<FeatureSnapshot<ProgressionSnapshot>> ReadCachedAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken) =>
            ReadSnapshotAsync(EnsureOwner(requestedOwner), SnapshotFreshness.Stale, cancellationToken);

        /// <summary>Commits the game supplied outcome and immutable command atomically. No local rule check, checkpoint write, progress increment, or reward occurs here.</summary>
        public Task<FeatureSnapshot<ProgressionSnapshot>> CompleteAsync(ProgressionCompletionRequest request, CancellationToken cancellationToken) =>
            CompleteAsync(request, (transaction, localRevision) => { }, cancellationToken);

        /// <summary>
        /// Commits a game-owned checkpoint/progress mutation in the same transaction as the SDK pending projection,
        /// allocated sequence and immutable outbox command. The callback is synchronous and transaction-only: it must
        /// not retain the transaction, start a nested transaction, publish UI, perform external side effects, or allocate
        /// authoritative platform value. Exact idempotent replay skips the callback.
        /// </summary>
        public async Task<FeatureSnapshot<ProgressionSnapshot>> CompleteAsync(ProgressionCompletionRequest request,
            Action<ILocalStorageTransaction, long> applyGameState, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (applyGameState == null) throw new ArgumentNullException(nameof(applyGameState));
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
                    applyGameState(transaction, localRevision);
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
        /// <remarks>
        /// At the installed revision, an identical projection may acknowledge further accepted operations as a new evidence group; the projection itself is never rewritten.
        /// </remarks>
        public async Task ApplyConfirmedAsync(ProgressionConfirmation confirmation, CancellationToken cancellationToken)
        {
            if (confirmation == null) throw new ArgumentNullException(nameof(confirmation));
            var exactOwner = EnsureOwner(confirmation.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                if (current.Confirmed != null && confirmation.Projection.Revision < current.Confirmed.Revision) return;
                if (current.Confirmed != null && confirmation.Projection.Revision == current.Confirmed.Revision)
                {
                    var priorEvidence = current.Confirmations.Where(value => value.ProjectionRevision == confirmation.Projection.Revision).ToArray();
                    if (!Equivalent(current.Confirmed, confirmation.Projection) ||
                        (priorEvidence.Length == 0 && current.LatestEvidenceRevision != confirmation.Projection.Revision))
                        throw new ProgressionConflictException("A same-revision progression pull must exactly replay its immutable projection and confirmation evidence.");
                    if (confirmations is ICompactingProgressionConfirmationEvidenceStore compacting &&
                        await compacting.MatchesAsync(exactOwner, new ProgressionConfirmationEvidence(
                            confirmation.Projection.Revision, confirmation.ConfirmedOperationIds), cancellationToken).ConfigureAwait(false)) return;
                    if (priorEvidence.Any(value => SameOperationSet(value.OperationIds, confirmation.ConfirmedOperationIds))) return;
                    if (confirmation.ConfirmedOperationIds.Count == 0)
                        throw new ProgressionConflictException("A same-revision acknowledgement must name at least one accepted operation.");
                    ValidateNewConfirmation(current.Pending, current.Confirmations, confirmation);
                    await transactions.ExecuteAsync(scope, transaction =>
                    {
                        confirmations.ValidateAndInsert(transaction, exactOwner, new ProgressionConfirmationEvidence(confirmation.Projection.Revision, confirmation.ConfirmedOperationIds), codec);
                        return true;
                    }, cancellationToken).ConfigureAwait(false);
                    return;
                }
                ValidateNewConfirmation(current.Pending, current.Confirmations, confirmation);
                await transactions.ExecuteAsync(scope, transaction =>
                {
                    ApplyConfirmedProjection(transaction, exactOwner, current.Confirmed == null ? (long?)null : current.Confirmed.Revision, confirmation);
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
                if (current.Confirmations.SelectMany(value => value.OperationIds).Contains(operationId))
                    throw new ProgressionConflictException("A pull-confirmed completion cannot be replaced by a rejection.");
                var retained = current.Pending.Where(value => value.OperationId != operationId).ToArray();
                if (retained.Length == current.Pending.Count) throw new ProgressionConflictException("The rejected receipt does not match a pending completion.");
                await WritePendingAsync(exactOwner, retained, current.PendingRevision, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose() { if (disposed) return; disposed = true; mutation.Dispose(); }

        /// <summary>
        /// Stages a confirmed projection and immutable operation evidence in the caller's private-group transaction.
        /// The caller supplies the durable projection revision observed by that same group; null means the projection
        /// is absent, while zero is a real server revision. Equal/lower revisions are never idempotently overwritten.
        /// This method does not advance a cursor.
        /// </summary>
        public void ApplyConfirmedProjection(ILocalStorageTransaction transaction, ScopedOwnerContext requestedOwner,
            long? durablePriorRevision, ProgressionConfirmation confirmation)
        {
            var exactOwner = EnsureOwner(requestedOwner);
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (confirmation == null) throw new ArgumentNullException(nameof(confirmation));
            if (confirmation.Owner != exactOwner) throw new ProgressionOwnerMismatchException();
            if (!transaction.Scope.Equals(scope)) throw new StorageException(StorageFailure.InvalidOwner, "The progression transaction belongs to another scope.");
            if (durablePriorRevision.HasValue && durablePriorRevision.Value < 0) throw new ArgumentOutOfRangeException(nameof(durablePriorRevision));
            if (durablePriorRevision.HasValue && confirmation.Projection.Revision <= durablePriorRevision.Value)
                throw new ProgressionConflictException("A borrowed progression projection must advance its durable revision.");
            var encoded = codec.EncodeConfirmed(confirmation.Projection); ValidatePayload(encoded, "confirmed progression");
            confirmations.ValidateAndInsert(transaction, exactOwner, new ProgressionConfirmationEvidence(confirmation.Projection.Revision, confirmation.ConfirmedOperationIds), codec);
            state.Upsert(transaction, new DurableFeatureMutation(exactOwner, Namespace, ConfirmedKey, confirmation.Projection.Revision,
                confirmation.Projection.ConfirmedAtMilliseconds, encoded, Array.Empty<byte>()));
        }

        private async Task<FeatureSnapshot<ProgressionSnapshot>> ReadSnapshotAsync(ScopedOwnerContext exactOwner, SnapshotFreshness freshness, CancellationToken cancellationToken)
        {
            var durable = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
            if (durable.Confirmed == null && durable.Pending.Count == 0)
                return new FeatureSnapshot<ProgressionSnapshot>(exactOwner, 0, FeatureSnapshotState.Missing, SnapshotFreshness.Missing, null, null, null);
            var visiblePending = SuppressConfirmed(durable.Pending, durable.Confirmations);
            if (durable.Confirmed == null && visiblePending.Count == 0)
                return new FeatureSnapshot<ProgressionSnapshot>(exactOwner, 0, FeatureSnapshotState.Missing, SnapshotFreshness.Missing, null, null, null);
            var value = new ProgressionSnapshot(durable.Confirmed, visiblePending);
            var revision = durable.Confirmed?.Revision ?? 0;
            if (visiblePending.Count != 0)
                return new FeatureSnapshot<ProgressionSnapshot>(exactOwner, revision, FeatureSnapshotState.Pending, freshness, value, durable.Confirmed?.ConfirmedAtMilliseconds, null);
            return new FeatureSnapshot<ProgressionSnapshot>(exactOwner, revision, freshness == SnapshotFreshness.Current ? FeatureSnapshotState.Available : FeatureSnapshotState.Stale,
                freshness, value, durable.Confirmed!.ConfirmedAtMilliseconds, null);
        }

        private async Task<(ProgressionConfirmedProjection? Confirmed, IReadOnlyList<PendingProgressionCompletion> Pending, long PendingRevision, IReadOnlyList<ProgressionConfirmationEvidence> Confirmations, long? LatestEvidenceRevision)> ReadDurableAsync(ScopedOwnerContext exactOwner, CancellationToken cancellationToken)
        {
            if (confirmations is ICompactingProgressionConfirmationEvidenceStore normalized)
                await normalized.EnsureNormalizedAsync(exactOwner, cancellationToken).ConfigureAwait(false);
            var confirmed = await state.ReadAsync(exactOwner, Namespace, ConfirmedKey, cancellationToken).ConfigureAwait(false);
            var pending = await state.ReadAsync(exactOwner, Namespace, PendingKey, cancellationToken).ConfigureAwait(false);
            ProgressionConfirmedProjection? decodedConfirmed = null;
            if (confirmed != null)
            {
                decodedConfirmed = codec.DecodeConfirmed(confirmed.Revision, confirmed.ConfirmedAtMilliseconds, confirmed.CopyPayload());
                if (decodedConfirmed == null || decodedConfirmed.Revision != confirmed.Revision || decodedConfirmed.ConfirmedAtMilliseconds != confirmed.ConfirmedAtMilliseconds)
                    throw new ProgressionConflictException("The progression codec returned an invalid confirmed projection.");
            }
            IReadOnlyList<PendingProgressionCompletion> decodedPending;
            if (pending == null || pending.PayloadLength == 0) decodedPending = Array.Empty<PendingProgressionCompletion>();
            else
            {
                decodedPending = codec.DecodePending(pending.Revision, pending.CopyPayload());
                if (decodedPending == null || decodedPending.Any(value => value == null) || decodedPending.Any(value => value.LocalRevision > pending.Revision) ||
                    decodedPending.GroupBy(value => value.OperationId).Any(group => group.Count() != 1) ||
                    decodedPending.GroupBy(value => value.BusinessSource, StringComparer.Ordinal).Any(group => group.Count() != 1))
                    throw new ProgressionConflictException("The progression codec returned invalid pending completions.");
            }
            ProgressionConfirmationEvidenceHead? head = confirmations is ICompactingProgressionConfirmationEvidenceStore compacting
                ? await compacting.ReadActiveAsync(exactOwner, decodedPending.Select(value => value.OperationId).ToArray(), cancellationToken).ConfigureAwait(false)
                : null;
            var evidence = head?.ActiveGroups ?? await confirmations.ReadAsync(exactOwner, cancellationToken).ConfigureAwait(false);
            ValidateEvidence(evidence);
            long? latestEvidenceRevision = head?.LatestRevision ?? (evidence.Count == 0 ? (long?)null : evidence.Max(value => value.ProjectionRevision));
            if (latestEvidenceRevision.HasValue && (decodedConfirmed == null || latestEvidenceRevision.Value > decodedConfirmed.Revision))
                throw new ProgressionConflictException("Confirmation evidence cannot lead the durable confirmed projection.");
            var raw = decodedPending.ToArray();
            foreach (var operationId in evidence.SelectMany(value => value.OperationIds))
            {
                var matches = raw.Where(value => value.OperationId == operationId).ToArray();
                if (matches.Length != 1 || matches[0].Status != ProgressionPendingStatus.AcceptedAwaitingPull)
                    throw new ProgressionConflictException("Confirmation evidence does not match one retained accepted-awaiting-pull completion.");
            }
            return (decodedConfirmed, raw, pending?.Revision ?? 0, evidence, latestEvidenceRevision);
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
        private static bool SameOperationSet(IReadOnlyList<OperationId> left, IReadOnlyList<OperationId> right) => left.Count == right.Count && left.All(value => right.Contains(value));
        private static IReadOnlyList<PendingProgressionCompletion> SuppressConfirmed(IReadOnlyList<PendingProgressionCompletion> pending, IReadOnlyList<ProgressionConfirmationEvidence> evidence)
        {
            var confirmed = new HashSet<OperationId>(evidence.SelectMany(value => value.OperationIds));
            return pending.Where(value => !confirmed.Contains(value.OperationId)).ToArray();
        }
        private static void ValidateEvidence(IReadOnlyList<ProgressionConfirmationEvidence>? evidence)
        {
            if (evidence == null || evidence.Count > 1024 || evidence.Any(value => value == null) ||
                evidence.Zip(evidence.Skip(1), (left, right) => left.ProjectionRevision <= right.ProjectionRevision).Any(value => !value) ||
                evidence.SelectMany(value => value.OperationIds).Count() > 1024 || evidence.SelectMany(value => value.OperationIds).GroupBy(value => value).Any(group => group.Count() != 1))
                throw new ProgressionConflictException("The progression confirmation evidence is invalid or exceeds its durable bound.");
        }
        private static void ValidateNewConfirmation(IReadOnlyList<PendingProgressionCompletion> pending, IReadOnlyList<ProgressionConfirmationEvidence> evidence, ProgressionConfirmation confirmation)
        {
            foreach (var operationId in confirmation.ConfirmedOperationIds)
            {
                var matches = pending.Where(value => value.OperationId == operationId).ToArray();
                if (matches.Length != 1 || matches[0].Status != ProgressionPendingStatus.AcceptedAwaitingPull)
                    throw new ProgressionConflictException("A pull confirmation may name only one retained accepted-awaiting-pull completion.");
                if (evidence.SelectMany(value => value.OperationIds).Contains(operationId))
                    throw new ProgressionConflictException("A pull confirmation cannot replace immutable operation evidence.");
            }
        }
    }
}
