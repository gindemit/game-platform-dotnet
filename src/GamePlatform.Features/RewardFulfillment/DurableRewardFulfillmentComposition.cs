#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.RewardFulfillment;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;

namespace GamePlatform.Features.RewardFulfillment
{
    /// <summary>Production durable CL-107 composition over the caller-owned serialized store and transaction executor.</summary>
    public static class DurableRewardFulfillmentComposition
    {
        public static RewardFulfillmentService Create(ScopedOwnerContext owner, StorageScope scope, IDurableFeatureStateStore state, ISerializedStorageExecutor transactions, Func<long> nowMilliseconds)
        {
            if (state == null) throw new ArgumentNullException(nameof(state)); if (transactions == null) throw new ArgumentNullException(nameof(transactions)); if (nowMilliseconds == null) throw new ArgumentNullException(nameof(nowMilliseconds));
            var codec = new DurableRewardFulfillmentStateCodec();
            return new RewardFulfillmentService(owner, scope, state, transactions, codec, new DurableClaimStore(owner, scope, state, transactions, nowMilliseconds), new DurableEvidenceStore(state), nowMilliseconds);
        }
        private sealed class DurableClaimStore : IRewardPresentationClaimStore
        {
            private readonly ScopedOwnerContext captured; private readonly StorageScope scope; private readonly IDurableFeatureStateStore state; private readonly ISerializedStorageExecutor transactions; private readonly Func<long> now;
            public DurableClaimStore(ScopedOwnerContext captured, StorageScope scope, IDurableFeatureStateStore state, ISerializedStorageExecutor transactions, Func<long> now) { this.captured = captured; this.scope = scope; this.state = state; this.transactions = transactions; this.now = now; }
            public Task<bool> TryClaimAsync(ScopedOwnerContext owner, Guid grantId, Action<ILocalStorageTransaction> onClaim, CancellationToken token)
            {
                if (owner != captured) throw new RewardFulfillmentOwnerMismatchException(); if (grantId == Guid.Empty) throw new ArgumentException("A grant identity is required.", nameof(grantId)); if (onClaim == null) throw new ArgumentNullException(nameof(onClaim));
                return transactions.ExecuteAsync(scope, transaction => { var key = "presentation/" + grantId.ToString("N"); if (state.Read(transaction, owner, "reward-fulfillment", key) != null) return false; state.Upsert(transaction, new DurableFeatureMutation(owner, "reward-fulfillment", key, 1, now(), new byte[] { 1 }, Array.Empty<byte>())); onClaim(transaction); return true; }, token);
            }
        }
        private sealed class DurableEvidenceStore : IRewardProjectionEvidenceStore
        {
            private readonly IDurableFeatureStateStore state; public DurableEvidenceStore(IDurableFeatureStateStore state) { this.state = state; }
            public void InsertOrVerify(ILocalStorageTransaction transaction, ScopedOwnerContext owner, Guid grantId, long feedRevision, long recordedAtMilliseconds, byte[] payload)
            {
                if (payload == null || payload.Length == 0) throw new ArgumentException("Canonical evidence is required.", nameof(payload)); var key = "evidence/" + grantId.ToString("N"); var existing = state.Read(transaction, owner, "reward-fulfillment", key);
                if (existing == null) { state.Upsert(transaction, new DurableFeatureMutation(owner, "reward-fulfillment", key, feedRevision, recordedAtMilliseconds, payload, Array.Empty<byte>())); return; }
                if (existing.Revision != feedRevision || !Same(existing.CopyPayload(), payload)) throw new RewardFulfillmentConflictException("A grant cannot replace immutable projection evidence.");
            }
            private static bool Same(byte[] left, byte[] right) { if (left.Length != right.Length) return false; var difference = 0; for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i]; return difference == 0; }
        }
    }

    /// <summary>Versioned deterministic local-state codec. It carries receipt/evidence facts only and never grants value.</summary>
    public sealed class DurableRewardFulfillmentStateCodec : IRewardFulfillmentStateCodec
    {
        private const int Version = 1;
        public byte[] EncodeRecord(RewardPresentationRecord value) => Write(writer => { writer.Write(Version); GuidValue(writer, value.OperationId.Value); Text(writer, value.BusinessSource); writer.Write((byte)value.Status); writer.Write(value.Presented); NullableText(writer, value.RejectionCode); writer.Write(value.Receipt != null); if (value.Receipt != null) Receipt(writer, value.Receipt); });
        public RewardPresentationRecord DecodeRecord(long revision, ReadOnlySpan<byte> payload) => Read(payload, reader => { Require(reader.ReadInt32() == Version); var operation = new OperationId(GuidValue(reader)); var source = Text(reader, 256); var status = (RewardPresentationStatus)reader.ReadByte(); var presented = reader.ReadBoolean(); var rejection = NullableText(reader, 128); var receipt = reader.ReadBoolean() ? Receipt(reader, source) : null; return new RewardPresentationRecord(operation, source, status, receipt, rejection, presented); });
        public byte[] EncodeGroup(RewardProjectionGroup value) => Write(writer => { writer.Write(Version); GuidValue(writer, value.OperationId.Value); GuidValue(writer, value.GrantId); Text(writer, value.BusinessSource); writer.Write(value.FeedRevision); writer.Write(value.Lines.Count); foreach (var line in value.Lines) { writer.Write(line.LineIndex); writer.Write((byte)line.Kind); Text(writer, line.ResourceId.Value); writer.Write(line.ProjectionRevision); } });
        public RewardProjectionGroup DecodeGroup(ScopedOwnerContext owner, long revision, ReadOnlySpan<byte> payload) => Read(payload, reader => { Require(reader.ReadInt32() == Version); var operation = new OperationId(GuidValue(reader)); var grant = GuidValue(reader); var source = Text(reader, 256); var feed = reader.ReadInt64(); var count = Count(reader, 64); var lines = new List<RewardProjectionLine>(count); for (var i = 0; i < count; i++) lines.Add(new RewardProjectionLine(reader.ReadInt32(), (RewardReceiptLineKind)reader.ReadByte(), new PlatformId(Text(reader, 128)), reader.ReadInt64())); return new RewardProjectionGroup(owner, operation, grant, source, feed, lines); });
        private static void Receipt(BinaryWriter writer, RewardReceipt value) { GuidValue(writer, value.GrantId); GuidValue(writer, value.OriginatingOperationId.Value); writer.Write(value.FeedRevision); writer.Write(value.RecordedAtMilliseconds); Text(writer, value.PlanId); writer.Write(value.PlanVersion); writer.Write(value.Lines.Count); foreach (var line in value.Lines) { writer.Write(line.LineIndex); writer.Write(line.DefinitionVersion); writer.Write((byte)line.Kind); Text(writer, line.ResourceId.Value); writer.Write(line.Quantity.HasValue); if (line.Quantity.HasValue) writer.Write(line.Quantity.Value); writer.Write(line.ExpiresAtServerMilliseconds.HasValue); if (line.ExpiresAtServerMilliseconds.HasValue) writer.Write(line.ExpiresAtServerMilliseconds.Value); } }
        private static RewardReceipt Receipt(BinaryReader reader, string source) { var grant = GuidValue(reader); var operation = new OperationId(GuidValue(reader)); var feed = reader.ReadInt64(); var recorded = reader.ReadInt64(); var plan = Text(reader, 128); var version = reader.ReadInt32(); var count = Count(reader, 64); var lines = new List<RewardReceiptLine>(count); for (var i = 0; i < count; i++) { var index = reader.ReadInt32(); var definition = reader.ReadInt32(); var kind = (RewardReceiptLineKind)reader.ReadByte(); var resource = new PlatformId(Text(reader, 128)); long? quantity = reader.ReadBoolean() ? reader.ReadInt64() : (long?)null; long? expiry = reader.ReadBoolean() ? reader.ReadInt64() : (long?)null; lines.Add(new RewardReceiptLine(index, definition, kind, resource, quantity, expiry)); } return new RewardReceipt(grant, operation, feed, recorded, source, plan, version, lines); }
        private static byte[] Write(Action<BinaryWriter> write) { using var stream = new MemoryStream(); using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true)) write(writer); return stream.ToArray(); }
        private static T Read<T>(ReadOnlySpan<byte> payload, Func<BinaryReader, T> read) { try { using var stream = new MemoryStream(payload.ToArray(), false); using var reader = new BinaryReader(stream, new UTF8Encoding(false, true), true); var value = read(reader); Require(stream.Position == stream.Length); return value; } catch (RewardFulfillmentConflictException) { throw; } catch (Exception error) { throw new RewardFulfillmentConflictException("Durable reward state is invalid: " + error.GetType().Name); } }
        private static void GuidValue(BinaryWriter writer, Guid value) => writer.Write(value.ToByteArray()); private static Guid GuidValue(BinaryReader reader) { var value = reader.ReadBytes(16); Require(value.Length == 16); return new Guid(value); }
        private static void Text(BinaryWriter writer, string value) { var bytes = new UTF8Encoding(false, true).GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }
        private static string Text(BinaryReader reader, int max) { var length = reader.ReadInt32(); Require(length >= 0 && length <= max * 4); var bytes = reader.ReadBytes(length); Require(bytes.Length == length); var value = new UTF8Encoding(false, true).GetString(bytes); Require(value.Length <= max); return value; }
        private static void NullableText(BinaryWriter writer, string? value) { writer.Write(value != null); if (value != null) Text(writer, value); }
        private static string? NullableText(BinaryReader reader, int max) => reader.ReadBoolean() ? Text(reader, max) : null;
        private static int Count(BinaryReader reader, int maximum) { var value = reader.ReadInt32(); Require(value > 0 && value <= maximum); return value; }
        private static void Require(bool condition) { if (!condition) throw new RewardFulfillmentConflictException("Durable reward state is invalid."); }
    }
}
