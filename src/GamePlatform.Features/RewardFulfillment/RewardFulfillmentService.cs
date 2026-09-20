#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.RewardFulfillment;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;

namespace GamePlatform.Features.RewardFulfillment
{
    /// <summary>Coordinates durable presentation of already server-issued reward receipts. It never writes Wallet, Inventory, Entitlements or an outbox command.</summary>
    public sealed class RewardFulfillmentService : IDisposable
    {
        private const string Namespace = "reward-fulfillment";
        private readonly ScopedOwnerContext owner;
        private readonly StorageScope scope;
        private readonly IDurableFeatureStateStore state;
        private readonly ISerializedStorageExecutor transactions;
        private readonly IRewardFulfillmentStateCodec codec;
        private readonly IRewardPresentationClaimStore claims;
        private readonly IRewardProjectionEvidenceStore evidence;
        private readonly Func<long> nowMilliseconds;
        private readonly SemaphoreSlim mutation = new SemaphoreSlim(1, 1);
        private bool disposed;

        public RewardFulfillmentService(ScopedOwnerContext owner, StorageScope scope, IDurableFeatureStateStore state,
            ISerializedStorageExecutor transactions, IRewardFulfillmentStateCodec codec, IRewardPresentationClaimStore claims,
            IRewardProjectionEvidenceStore evidence, Func<long> nowMilliseconds)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!Matches(scope, owner.Owner)) throw new ArgumentException("The reward presentation scope does not match the captured owner.", nameof(scope));
            this.owner = owner; this.scope = scope; this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions)); this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
            this.evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
            this.nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
        }

        /// <summary>Durably records an operation as awaiting a terminal receipt. This is presentation state only, and creates no local value.</summary>
        public async Task<FeatureSnapshot<RewardPresentationRecord>> BeginPendingAsync(RewardPresentationQuery query, string businessSource, CancellationToken cancellationToken)
        {
            var exact = EnsureQuery(query); ValidateBusinessSource(businessSource, nameof(businessSource));
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadOperationAsync(exact, cancellationToken).ConfigureAwait(false);
                if (current != null)
                {
                    if (!string.Equals(current.Value.Record.BusinessSource, businessSource, StringComparison.Ordinal)) throw new RewardFulfillmentConflictException("An operation cannot be rebound to another business source.");
                    return Snapshot(exact.Owner, current.Value.Revision, current.Value.Record);
                }
                await WriteRecordAsync(exact.Owner, new RewardPresentationRecord(exact.OperationId, businessSource, RewardPresentationStatus.Pending, null, null, false), 1, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadAsync(exact, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Records a trusted accepted receipt. It stays pending until matching ordered-feed group evidence arrives.</summary>
        public async Task<FeatureSnapshot<RewardPresentationRecord>> ObserveAcceptedReceiptAsync(RewardReceiptObservation observation, CancellationToken cancellationToken)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation)); EnsureOwner(observation.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await transactions.ExecuteAsync(scope, transaction => { StageAcceptedReceipt(transaction, observation); return true; }, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadAsync(new RewardPresentationQuery(observation.Owner, observation.OperationId), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Persists matching projection-group evidence after component services have installed their server-confirmed values. It never applies those values itself.</summary>
        public async Task<FeatureSnapshot<RewardPresentationRecord>> ObserveProjectionGroupAsync(RewardProjectionGroup group, CancellationToken cancellationToken)
        {
            if (group == null) throw new ArgumentNullException(nameof(group)); EnsureOwner(group.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await transactions.ExecuteAsync(scope, transaction => { StageProjectionGroupAndConfirm(transaction, group); return true; }, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadAsync(new RewardPresentationQuery(group.Owner, group.OperationId), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Stages validated feed-group evidence in the caller's projection/cursor transaction. It never advances a cursor or mutates value projections itself.</summary>
        public void StageProjectionGroup(ILocalStorageTransaction transaction, RewardProjectionGroup group)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (group == null) throw new ArgumentNullException(nameof(group));
            EnsureOwner(group.Owner);
            if (!transaction.Scope.Equals(scope)) throw new RewardFulfillmentOwnerMismatchException();
            var payload = codec.EncodeGroup(group); ValidatePayload(payload);
            evidence.InsertOrVerify(transaction, group.Owner, group.GrantId, group.FeedRevision, Now(), payload);
        }

        /// <summary>Stages receipt presentation in an existing caller-owned transaction. Internal composition uses this with correlation facts so neither can commit alone.</summary>
        internal void StageAcceptedReceipt(ILocalStorageTransaction transaction, RewardReceiptObservation observation)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            EnsureOwner(observation.Owner); EnsureTransaction(transaction);
            var query = new RewardPresentationQuery(observation.Owner, observation.OperationId);
            var current = ReadOperation(transaction, query);
            if (current != null && !string.Equals(current.Value.Record.BusinessSource, observation.Receipt.BusinessSource, StringComparison.Ordinal))
                throw new RewardFulfillmentConflictException("An operation receipt conflicts with its durable business source.");
            if (current != null && current.Value.Record.Status == RewardPresentationStatus.Rejected) throw new RewardFulfillmentConflictException("A rejected operation cannot become accepted.");
            if (current != null && current.Value.Record.Receipt != null && !SameReceipt(current.Value.Record.Receipt, observation.Receipt))
                throw new RewardFulfillmentConflictException("The retained receipt conflicts with the accepted receipt.");
            if (current != null && current.Value.Record.Receipt != null) return;

            var existingEvidence = ReadEvidence(transaction, observation.Owner, observation.Receipt.GrantId);
            var confirmed = existingEvidence != null && Matches(observation.Receipt, existingEvidence.Value.Group);
            if (existingEvidence != null && !confirmed) throw new RewardFulfillmentConflictException("The stored projection group does not prove this receipt.");
            var record = new RewardPresentationRecord(observation.OperationId, observation.Receipt.BusinessSource,
                confirmed ? RewardPresentationStatus.Confirmed : RewardPresentationStatus.AcceptedAwaitingPull, observation.Receipt, null, false);
            WriteRecord(transaction, observation.Owner, record, NextRevision(current.HasValue ? current.Value.Revision : (long?)null, observation.Receipt.FeedRevision));
        }

        /// <summary>Stages immutable evidence and its matching confirmation together in an existing caller-owned transaction.</summary>
        internal void StageProjectionGroupAndConfirm(ILocalStorageTransaction transaction, RewardProjectionGroup group)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (group == null) throw new ArgumentNullException(nameof(group));
            EnsureOwner(group.Owner); EnsureTransaction(transaction);
            StageProjectionGroup(transaction, group);
            var current = ReadOperation(transaction, new RewardPresentationQuery(group.Owner, group.OperationId));
            if (current == null || current.Value.Record.Receipt == null) return;
            if (!Matches(current.Value.Record.Receipt, group)) throw new RewardFulfillmentConflictException("The projection group is partial or does not match the receipt.");
            if (current.Value.Record.Status == RewardPresentationStatus.AcceptedAwaitingPull)
            {
                var confirmed = new RewardPresentationRecord(current.Value.Record.OperationId, current.Value.Record.BusinessSource,
                    RewardPresentationStatus.Confirmed, current.Value.Record.Receipt, null, current.Value.Record.Presented);
                WriteRecord(transaction, group.Owner, confirmed, NextRevision(current.Value.Revision, group.FeedRevision));
            }
        }

        public async Task<FeatureSnapshot<RewardPresentationRecord>> ObserveRejectedAsync(RewardRejection rejection, CancellationToken cancellationToken)
        {
            if (rejection == null) throw new ArgumentNullException(nameof(rejection)); EnsureOwner(rejection.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var query = new RewardPresentationQuery(rejection.Owner, rejection.OperationId); var current = await ReadOperationAsync(query, cancellationToken).ConfigureAwait(false);
                if (current != null && !string.Equals(current.Value.Record.BusinessSource, rejection.BusinessSource, StringComparison.Ordinal)) throw new RewardFulfillmentConflictException("A rejection conflicts with the durable business source.");
                if (current != null && current.Value.Record.Receipt != null) throw new RewardFulfillmentConflictException("An accepted receipt cannot be replaced by a rejection.");
                if (current != null && current.Value.Record.Status == RewardPresentationStatus.Rejected && !string.Equals(current.Value.Record.RejectionCode, rejection.Code, StringComparison.Ordinal)) throw new RewardFulfillmentConflictException("The retained rejection conflicts with the response.");
                await WriteRecordAsync(rejection.Owner, new RewardPresentationRecord(rejection.OperationId, rejection.BusinessSource, RewardPresentationStatus.Rejected, null, rejection.Code, false), NextRevision(current.HasValue ? current.Value.Revision : (long?)null, 0), cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadAsync(new RewardPresentationQuery(rejection.Owner, rejection.OperationId), cancellationToken).ConfigureAwait(false);
        }

        public async Task<FeatureSnapshot<RewardPresentationRecord>> ReadAsync(RewardPresentationQuery query, CancellationToken cancellationToken)
        {
            var exact = EnsureQuery(query); var current = await ReadOperationAsync(exact, cancellationToken).ConfigureAwait(false);
            return current == null ? new FeatureSnapshot<RewardPresentationRecord>(exact.Owner, 0, FeatureSnapshotState.Missing, SnapshotFreshness.Missing, null, null, null) : Snapshot(exact.Owner, current.Value.Revision, current.Value.Record);
        }

        /// <summary>Atomically leases one confirmed receipt for UI display. This separate dedupe is intentionally independent of server business-source uniqueness.</summary>
        public async Task<RewardPresentationRecord?> TryClaimPresentationAsync(RewardPresentationQuery query, CancellationToken cancellationToken)
        {
            var exact = EnsureQuery(query); await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadOperationAsync(exact, cancellationToken).ConfigureAwait(false);
                if (current == null || current.Value.Record.Status != RewardPresentationStatus.Confirmed || current.Value.Record.Presented) return null;
                var presented = new RewardPresentationRecord(current.Value.Record.OperationId, current.Value.Record.BusinessSource, RewardPresentationStatus.Confirmed, current.Value.Record.Receipt, null, true);
                var claimed = await claims.TryClaimAsync(exact.Owner, current.Value.Record.Receipt!.GrantId, transaction =>
                {
                    var recordPayload = codec.EncodeRecord(presented); ValidatePayload(recordPayload);
                    state.Upsert(transaction, new DurableFeatureMutation(exact.Owner, Namespace, OperationKey(presented.OperationId), NextRevision(current.Value.Revision, 0), Now(), recordPayload, Array.Empty<byte>()));
                }, cancellationToken).ConfigureAwait(false);
                return claimed ? presented : null;
            }
            finally { mutation.Release(); }
        }

        public void Dispose() { if (disposed) return; disposed = true; mutation.Dispose(); }

        private async Task<(RewardPresentationRecord Record, long Revision)?> ReadOperationAsync(RewardPresentationQuery query, CancellationToken cancellationToken)
        {
            var stored = await state.ReadAsync(query.Owner, Namespace, OperationKey(query.OperationId), cancellationToken).ConfigureAwait(false);
            return DecodeOperation(query, stored);
        }
        private (RewardPresentationRecord Record, long Revision)? ReadOperation(ILocalStorageTransaction transaction, RewardPresentationQuery query) =>
            DecodeOperation(query, state.Read(transaction, query.Owner, Namespace, OperationKey(query.OperationId)));
        private (RewardProjectionGroup Group, long Revision)? ReadEvidence(ILocalStorageTransaction transaction, ScopedOwnerContext exactOwner, Guid grantId) =>
            DecodeEvidence(exactOwner, grantId, state.Read(transaction, exactOwner, Namespace, EvidenceKey(grantId)));
        private (RewardPresentationRecord Record, long Revision)? DecodeOperation(RewardPresentationQuery query, DurableFeatureState? stored)
        {
            if (stored == null) return null;
            var decoded = codec.DecodeRecord(stored.Revision, stored.CopyPayload());
            if (decoded == null || decoded.OperationId != query.OperationId) throw new RewardFulfillmentConflictException("The reward state codec returned an invalid operation record.");
            return (decoded, stored.Revision);
        }
        private (RewardProjectionGroup Group, long Revision)? DecodeEvidence(ScopedOwnerContext exactOwner, Guid grantId, DurableFeatureState? stored)
        {
            if (stored == null) return null;
            var decoded = codec.DecodeGroup(exactOwner, stored.Revision, stored.CopyPayload());
            if (decoded == null || decoded.GrantId != grantId) throw new RewardFulfillmentConflictException("The reward state codec returned invalid projection evidence.");
            return (decoded, stored.Revision);
        }
        private Task WriteRecordAsync(ScopedOwnerContext exactOwner, RewardPresentationRecord record, long revision, CancellationToken cancellationToken) =>
            transactions.ExecuteAsync(scope, transaction => { WriteRecord(transaction, exactOwner, record, revision); return true; }, cancellationToken);
        private void WriteRecord(ILocalStorageTransaction transaction, ScopedOwnerContext exactOwner, RewardPresentationRecord record, long revision)
        {
            EnsureTransaction(transaction);
            var payload = codec.EncodeRecord(record); ValidatePayload(payload);
            state.Upsert(transaction, new DurableFeatureMutation(exactOwner, Namespace, OperationKey(record.OperationId), revision, Now(), payload, Array.Empty<byte>()));
        }
        private RewardPresentationQuery EnsureQuery(RewardPresentationQuery query) { if (query == null) throw new ArgumentNullException(nameof(query)); EnsureOwner(query.Owner); return query; }
        private void EnsureOwner(ScopedOwnerContext requested) { if (disposed) throw new ObjectDisposedException(nameof(RewardFulfillmentService)); if (requested != owner) throw new RewardFulfillmentOwnerMismatchException(); }
        private void EnsureTransaction(ILocalStorageTransaction transaction) { if (!transaction.Scope.Equals(scope)) throw new RewardFulfillmentOwnerMismatchException(); }
        private long Now() { var value = nowMilliseconds(); if (value < 0 || value > 253_402_300_799_999L) throw new ArgumentOutOfRangeException("nowMilliseconds"); return value; }
        private static long NextRevision(long? current, long observed) { if (!current.HasValue) return observed > 0 ? observed : 1; return current.Value == long.MaxValue ? throw new RewardFulfillmentConflictException("The receipt record revision is exhausted.") : Math.Max(current.Value + 1, observed); }
        private static FeatureSnapshot<RewardPresentationRecord> Snapshot(ScopedOwnerContext owner, long revision, RewardPresentationRecord record) =>
            record.Status == RewardPresentationStatus.Rejected ? new FeatureSnapshot<RewardPresentationRecord>(owner, revision, FeatureSnapshotState.Error, SnapshotFreshness.Stale, record, null, record.RejectionCode) :
            record.Status == RewardPresentationStatus.Confirmed ? new FeatureSnapshot<RewardPresentationRecord>(owner, revision, FeatureSnapshotState.Stale, SnapshotFreshness.Stale, record, record.Receipt!.RecordedAtMilliseconds, null) :
            new FeatureSnapshot<RewardPresentationRecord>(owner, revision, FeatureSnapshotState.Pending, SnapshotFreshness.Stale, record, record.Receipt?.RecordedAtMilliseconds, null);
        private static bool Matches(RewardReceipt receipt, RewardProjectionGroup group) => receipt.GrantId == group.GrantId && receipt.FeedRevision == group.FeedRevision && string.Equals(receipt.BusinessSource, group.BusinessSource, StringComparison.Ordinal) && receipt.Lines.Count == group.Lines.Count && receipt.Lines.All(line => group.Lines.Any(value => value.LineIndex == line.LineIndex && value.Kind == line.Kind && value.ResourceId.Equals(line.ResourceId)));
        private static bool SameReceipt(RewardReceipt left, RewardReceipt right) => left.GrantId == right.GrantId && left.OriginatingOperationId == right.OriginatingOperationId && left.FeedRevision == right.FeedRevision && left.RecordedAtMilliseconds == right.RecordedAtMilliseconds && string.Equals(left.BusinessSource, right.BusinessSource, StringComparison.Ordinal) && string.Equals(left.PlanId, right.PlanId, StringComparison.Ordinal) && left.PlanVersion == right.PlanVersion && left.Lines.Count == right.Lines.Count && left.Lines.All(line => right.Lines.Any(value => value.LineIndex == line.LineIndex && value.Kind == line.Kind && value.DefinitionVersion == line.DefinitionVersion && value.ResourceId.Equals(line.ResourceId) && value.Quantity == line.Quantity && value.ExpiresAtServerMilliseconds == line.ExpiresAtServerMilliseconds));
        private static string OperationKey(OperationId operationId) => "operation/" + operationId.ToString();
        private static string EvidenceKey(Guid grantId) => "evidence/" + grantId.ToString("N");
        private static void ValidatePayload(byte[] payload) { if (payload == null || payload.Length == 0 || payload.Length > 262_144) throw new RewardFulfillmentConflictException("The reward state codec produced an invalid payload."); }
        private static void ValidateBusinessSource(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || !System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._:-]{0,255}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) throw new ArgumentOutOfRangeException(parameter);
        }
        private static bool Matches(StorageScope scope, OwnerScope owner) => string.Equals(scope.BackendNamespace, owner.Backend.Value, StringComparison.Ordinal) && string.Equals(scope.AppId.Value, owner.AppId.ToString(), StringComparison.Ordinal) && string.Equals(scope.AccountId.Value, owner.UserId.ToString(), StringComparison.Ordinal);
    }
}
