#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.RewardFulfillment;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;

namespace GamePlatform.Features.RewardFulfillment
{
    /// <summary>One value projection actually installed from a committed feed group. It deliberately carries no quantity.</summary>
    public sealed class InstalledRewardProjection
    {
        public InstalledRewardProjection(RewardReceiptLineKind kind, PlatformId resourceId, long projectionRevision) { if (!Enum.IsDefined(typeof(RewardReceiptLineKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind)); if (!resourceId.IsValid) throw new ArgumentException("A resource is required.", nameof(resourceId)); if (projectionRevision < 0) throw new ArgumentOutOfRangeException(nameof(projectionRevision)); Kind = kind; ResourceId = resourceId; ProjectionRevision = projectionRevision; }
        public RewardReceiptLineKind Kind { get; }
        public PlatformId ResourceId { get; }
        public long ProjectionRevision { get; }
    }

    /// <summary>
    /// Durable join between an immutable receipt and the exact installed private-feed revision. Feed staging runs in
    /// the caller's projection/cursor transaction; confirmation runs only after that transaction commits. This type
    /// never increments a projection revision or applies receipt quantities to local value.
    /// </summary>
    public sealed class DurableRewardReceiptCorrelation
    {
        private const string Namespace = "reward-correlation"; private const string SnapshotKey = "snapshot"; private const string PendingKey = "pending"; private const int MaximumPending = 4096; private const int MaximumFeedProjections = 64; private const int MaximumSnapshotProjections = 1024; private readonly ScopedOwnerContext owner; private readonly StorageScope scope; private readonly IDurableFeatureStateStore state; private readonly ISerializedStorageExecutor transactions; private readonly RewardFulfillmentService fulfillment; private readonly Func<long> now; private readonly DurableRewardFulfillmentStateCodec codec = new DurableRewardFulfillmentStateCodec();
        public DurableRewardReceiptCorrelation(ScopedOwnerContext owner, StorageScope scope, IDurableFeatureStateStore state, ISerializedStorageExecutor transactions, RewardFulfillmentService fulfillment, Func<long> nowMilliseconds) { if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner)); this.owner = owner; this.scope = scope; this.state = state ?? throw new ArgumentNullException(nameof(state)); this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions)); this.fulfillment = fulfillment ?? throw new ArgumentNullException(nameof(fulfillment)); this.now = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds)); }
        public async Task ObserveReceiptAsync(RewardReceiptObservation observation, CancellationToken token)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation)); EnsureOwner(observation.Owner);
            await transactions.ExecuteAsync(scope, transaction =>
            {
                // Receipt facts, optional feed join, immutable evidence and the operation presentation are one durable unit.
                fulfillment.StageAcceptedReceipt(transaction, observation);
                var payload = codec.EncodeRecord(new RewardPresentationRecord(observation.OperationId, observation.Receipt.BusinessSource, RewardPresentationStatus.AcceptedAwaitingPull, observation.Receipt, null, false));
                InsertOrVerify(transaction, ReceiptKey(observation.Receipt.FeedRevision), observation.Receipt.FeedRevision, payload);
                var feed = state.Read(transaction, owner, Namespace, FeedKey(observation.Receipt.FeedRevision));
                if (feed != null) fulfillment.StageProjectionGroupAndConfirm(transaction, Join(observation, DecodeFeed(feed.CopyPayload(), MaximumFeedProjections), observation.Receipt.FeedRevision));
                else if (!TryConfirmFromSnapshot(transaction, observation, state.Read(transaction, owner, Namespace, SnapshotKey))) { var pending = ReadPending(transaction); if (pending.Add(observation.Receipt.FeedRevision)) WritePending(transaction, pending); }
                return true;
            }, token).ConfigureAwait(false);
        }
        /// <summary>Call after all listed component upserts are installed, inside the same feed/cursor transaction.</summary>
        public void StageInstalledFeed(ILocalStorageTransaction transaction, long feedRevision, IReadOnlyList<InstalledRewardProjection> projections)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction)); if (!transaction.Scope.Equals(scope)) throw new RewardFulfillmentOwnerMismatchException(); if (feedRevision <= 0) throw new ArgumentOutOfRangeException(nameof(feedRevision)); var exact = Validate(projections, MaximumFeedProjections); InsertOrVerify(transaction, FeedKey(feedRevision), feedRevision, EncodeFeed(exact)); var pending = ReadPending(transaction); if (pending.Remove(feedRevision)) WritePending(transaction, pending); var receipt = state.Read(transaction, owner, Namespace, ReceiptKey(feedRevision)); if (receipt != null) { var record = codec.DecodeRecord(receipt.Revision, receipt.CopyPayload()); if (record.Receipt == null) throw new RewardFulfillmentConflictException("Durable receipt correlation has no receipt."); fulfillment.StageProjectionGroupAndConfirm(transaction, Join(new RewardReceiptObservation(owner, record.OperationId, record.Receipt), exact, feedRevision)); }
        }
        /// <summary>Call after a complete snapshot with every installed value projection, inside its projection/cursor transaction.</summary>
        /// <remarks>A receipt whose feed revision the snapshot covers is confirmed against the snapshot's projection revisions.</remarks>
        public void StageInstalledSnapshot(ILocalStorageTransaction transaction, long committedThrough, IReadOnlyList<InstalledRewardProjection> projections)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction)); if (!transaction.Scope.Equals(scope)) throw new RewardFulfillmentOwnerMismatchException(); if (committedThrough < 0) throw new ArgumentOutOfRangeException(nameof(committedThrough)); var exact = Validate(projections, MaximumSnapshotProjections);
            var snapshot = state.Read(transaction, owner, Namespace, SnapshotKey);
            // The latest installed snapshot replaces the retained one, even at a lower revision.
            if (snapshot != null && snapshot.Revision > committedThrough) state.Delete(transaction, owner, Namespace, SnapshotKey);
            state.Upsert(transaction, new DurableFeatureMutation(owner, Namespace, SnapshotKey, committedThrough, Now(), EncodeFeed(exact), Array.Empty<byte>())); snapshot = state.Read(transaction, owner, Namespace, SnapshotKey);
            var pending = ReadPending(transaction); var remaining = new SortedSet<long>();
            foreach (var revision in pending)
            {
                var receipt = state.Read(transaction, owner, Namespace, ReceiptKey(revision)) ?? throw new RewardFulfillmentConflictException("A pending reward correlation has no receipt.");
                var record = codec.DecodeRecord(receipt.Revision, receipt.CopyPayload()); if (record.Receipt == null) throw new RewardFulfillmentConflictException("Durable receipt correlation has no receipt.");
                if (!TryConfirmFromSnapshot(transaction, new RewardReceiptObservation(owner, record.OperationId, record.Receipt), snapshot)) remaining.Add(revision);
            }
            if (remaining.Count != pending.Count) WritePending(transaction, remaining);
        }
        /// <summary>Call only after the feed transaction committed; exact replay is safe.</summary>
        public async Task ReconcileAsync(long feedRevision, CancellationToken token)
        {
            if (feedRevision <= 0) throw new ArgumentOutOfRangeException(nameof(feedRevision));
            // This is idempotent repair/notification for pre-existing state, not a required post-commit correctness step.
            await transactions.ExecuteAsync(scope, transaction =>
            {
                var receipt = state.Read(transaction, owner, Namespace, ReceiptKey(feedRevision)); var feed = state.Read(transaction, owner, Namespace, FeedKey(feedRevision)); if (receipt == null || feed == null) return true;
                var record = codec.DecodeRecord(receipt.Revision, receipt.CopyPayload()); if (record.Receipt == null) throw new RewardFulfillmentConflictException("Durable receipt correlation has no receipt.");
                var observation = new RewardReceiptObservation(owner, record.OperationId, record.Receipt); fulfillment.StageAcceptedReceipt(transaction, observation); fulfillment.StageProjectionGroupAndConfirm(transaction, Join(observation, DecodeFeed(feed.CopyPayload(), MaximumFeedProjections), feedRevision)); return true;
            }, token).ConfigureAwait(false);
        }
        private RewardProjectionGroup Join(RewardReceiptObservation observation, IReadOnlyList<InstalledRewardProjection> projections, long feedRevision)
        {
            if (observation.Receipt.FeedRevision != feedRevision || projections.Count != observation.Receipt.Lines.Count) throw new RewardFulfillmentConflictException("The exact installed feed revision does not match the receipt."); var remaining = projections.ToList(); var lines = new List<RewardProjectionLine>(); foreach (var receiptLine in observation.Receipt.Lines) { var matches = remaining.Where(value => value.Kind == receiptLine.Kind && value.ResourceId.Equals(receiptLine.ResourceId)).ToArray(); if (matches.Length != 1) throw new RewardFulfillmentConflictException("The installed feed group is partial or ambiguous."); var match = matches[0]; remaining.Remove(match); lines.Add(new RewardProjectionLine(receiptLine.LineIndex, receiptLine.Kind, receiptLine.ResourceId, match.ProjectionRevision)); }
            if (remaining.Count != 0) throw new RewardFulfillmentConflictException("The installed feed group contains unrelated value projections."); return new RewardProjectionGroup(owner, observation.OperationId, observation.Receipt.GrantId, observation.Receipt.BusinessSource, feedRevision, lines);
        }
        private bool TryConfirmFromSnapshot(ILocalStorageTransaction transaction, RewardReceiptObservation observation, DurableFeatureState? snapshot)
        {
            if (snapshot == null || snapshot.Revision < observation.Receipt.FeedRevision) return false; var installed = DecodeFeed(snapshot.CopyPayload(), MaximumSnapshotProjections); var covered = new List<InstalledRewardProjection>();
            foreach (var line in observation.Receipt.Lines) { var match = installed.FirstOrDefault(value => value.Kind == line.Kind && value.ResourceId.Equals(line.ResourceId)); if (match == null) return false; covered.Add(match); }
            if (!fulfillment.HasEvidence(transaction, observation.Receipt.GrantId)) fulfillment.StageProjectionGroupAndConfirm(transaction, Join(observation, covered, observation.Receipt.FeedRevision)); return true;
        }
        private SortedSet<long> ReadPending(ILocalStorageTransaction transaction)
        {
            var stored = state.Read(transaction, owner, Namespace, PendingKey); var result = new SortedSet<long>(); if (stored == null) return result;
            try { using var stream = new MemoryStream(stored.CopyPayload(), false); using var reader = new BinaryReader(stream); if (reader.ReadInt32() != 1) throw new InvalidDataException(); var count = reader.ReadInt32(); if (count < 0 || count > MaximumPending) throw new InvalidDataException(); for (var i = 0; i < count; i++) { var revision = reader.ReadInt64(); if (revision <= 0 || !result.Add(revision)) throw new InvalidDataException(); } if (stream.Position != stream.Length) throw new InvalidDataException(); return result; }
            catch (Exception error) { throw new RewardFulfillmentConflictException("Durable pending reward correlation is invalid: " + error.GetType().Name); }
        }
        private void WritePending(ILocalStorageTransaction transaction, SortedSet<long> revisions)
        {
            if (revisions.Count > MaximumPending) throw new RewardFulfillmentConflictException("Too many receipts await their installed feed revision."); var stored = state.Read(transaction, owner, Namespace, PendingKey); var next = stored == null ? 1 : checked(stored.Revision + 1);
            using var stream = new MemoryStream(); using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true)) { writer.Write(1); writer.Write(revisions.Count); foreach (var revision in revisions) writer.Write(revision); }
            state.Upsert(transaction, new DurableFeatureMutation(owner, Namespace, PendingKey, next, Now(), stream.ToArray(), Array.Empty<byte>()));
        }
        private void InsertOrVerify(ILocalStorageTransaction transaction, string key, long revision, byte[] payload) { var existing = state.Read(transaction, owner, Namespace, key); if (existing == null) { state.Upsert(transaction, new DurableFeatureMutation(owner, Namespace, key, revision, Now(), payload, Array.Empty<byte>())); return; } if (existing.Revision != revision || !Same(existing.CopyPayload(), payload)) throw new RewardFulfillmentConflictException("Durable reward correlation cannot be replaced."); }
        private static InstalledRewardProjection[] Validate(IReadOnlyList<InstalledRewardProjection> values, int maximum) { if (values == null) throw new ArgumentNullException(nameof(values)); if (values.Count > maximum || values.Any(value => value == null)) throw new ArgumentException("Installed reward projections must be bounded.", nameof(values)); var result = values.ToArray(); if (result.GroupBy(value => ((int)value.Kind) + ":" + value.ResourceId.Value, StringComparer.Ordinal).Any(group => group.Count() != 1)) throw new RewardFulfillmentConflictException("Installed reward projection identities must be unique."); return result; }
        private static byte[] EncodeFeed(IEnumerable<InstalledRewardProjection> values) { using var stream = new MemoryStream(); using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true)) { writer.Write(1); var array = values.ToArray(); writer.Write(array.Length); foreach (var value in array) { writer.Write((byte)value.Kind); var bytes = Encoding.UTF8.GetBytes(value.ResourceId.Value); writer.Write(bytes.Length); writer.Write(bytes); writer.Write(value.ProjectionRevision); } } return stream.ToArray(); }
        private static InstalledRewardProjection[] DecodeFeed(byte[] payload, int maximum) { try { using var stream = new MemoryStream(payload, false); using var reader = new BinaryReader(stream, new UTF8Encoding(false, true), true); if (reader.ReadInt32() != 1) throw new InvalidDataException(); var count = reader.ReadInt32(); if (count < 0 || count > maximum) throw new InvalidDataException(); var result = new InstalledRewardProjection[count]; for (var i = 0; i < count; i++) { var kind = (RewardReceiptLineKind)reader.ReadByte(); var length = reader.ReadInt32(); if (length <= 0 || length > 512) throw new InvalidDataException(); var bytes = reader.ReadBytes(length); if (bytes.Length != length) throw new InvalidDataException(); result[i] = new InstalledRewardProjection(kind, new PlatformId(new UTF8Encoding(false, true).GetString(bytes)), reader.ReadInt64()); } if (stream.Position != stream.Length) throw new InvalidDataException(); return Validate(result, maximum); } catch (RewardFulfillmentConflictException) { throw; } catch (Exception error) { throw new RewardFulfillmentConflictException("Durable feed correlation is invalid: " + error.GetType().Name); } }
        private void EnsureOwner(ScopedOwnerContext value) { if (value != owner) throw new RewardFulfillmentOwnerMismatchException(); }
        private long Now() { var value = now(); if (value < 0 || value > 253402300799999L) throw new ArgumentOutOfRangeException("nowMilliseconds"); return value; }
        private static string ReceiptKey(long revision) => "receipt/" + revision; private static string FeedKey(long revision) => "feed/" + revision; private static bool Same(byte[] left, byte[] right) { if (left.Length != right.Length) return false; var difference = 0; for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i]; return difference == 0; }
    }
}
