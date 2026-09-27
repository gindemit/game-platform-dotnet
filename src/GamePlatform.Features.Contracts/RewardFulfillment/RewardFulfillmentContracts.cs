#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{
    /// <summary>Durable UI state for a server-issued receipt. It never describes a locally granted balance or right.</summary>
    public enum RewardPresentationStatus { Pending = 0, AcceptedAwaitingPull = 1, Confirmed = 2, Rejected = 3 }
    public enum RewardReceiptLineKind { Currency = 0, ItemStack = 1, Entitlement = 2 }

    /// <summary>One immutable line from the bounded, trusted reward-receipt response. This is receipt evidence, not a client grant plan.</summary>
    public sealed class RewardReceiptLine
    {
        public RewardReceiptLine(int lineIndex, int definitionVersion, RewardReceiptLineKind kind, PlatformId resourceId, long? quantity, long? expiresAtServerMilliseconds)
        {
            if (lineIndex < 0 || lineIndex > 63) throw new ArgumentOutOfRangeException(nameof(lineIndex));
            if (definitionVersion <= 0) throw new ArgumentOutOfRangeException(nameof(definitionVersion));
            if (!Enum.IsDefined(typeof(RewardReceiptLineKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!resourceId.IsValid) throw new ArgumentException("A receipt resource identity is required.", nameof(resourceId));
            if (kind == RewardReceiptLineKind.Entitlement)
            {
                if (quantity.HasValue) throw new ArgumentException("Entitlement receipt lines carry no quantity.", nameof(quantity));
            }
            else if (!quantity.HasValue || quantity.Value <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (expiresAtServerMilliseconds.HasValue && (expiresAtServerMilliseconds.Value < 0 || expiresAtServerMilliseconds.Value > 253_402_300_799_999L))
                throw new ArgumentOutOfRangeException(nameof(expiresAtServerMilliseconds));
            if (kind != RewardReceiptLineKind.Entitlement && expiresAtServerMilliseconds.HasValue)
                throw new ArgumentException("Only entitlement lines carry an expiry.", nameof(expiresAtServerMilliseconds));
            LineIndex = lineIndex; DefinitionVersion = definitionVersion; Kind = kind; ResourceId = resourceId;
            Quantity = quantity; ExpiresAtServerMilliseconds = expiresAtServerMilliseconds;
        }
        public int LineIndex { get; }
        public int DefinitionVersion { get; }
        public RewardReceiptLineKind Kind { get; }
        public PlatformId ResourceId { get; }
        public long? Quantity { get; }
        public long? ExpiresAtServerMilliseconds { get; }
    }

    /// <summary>Immutable accepted backend receipt. Its source is the canonical backend business-source key, never a client-selected eligibility key.</summary>
    public sealed class RewardReceipt
    {
        private readonly RewardReceiptLine[] lines;
        public RewardReceipt(Guid grantId, OperationId originatingOperationId, long feedRevision, long recordedAtMilliseconds,
            string businessSource, string planId, int planVersion, IEnumerable<RewardReceiptLine> lines)
        {
            if (grantId == Guid.Empty) throw new ArgumentException("A grant identity is required.", nameof(grantId));
            if (!originatingOperationId.IsValid) throw new ArgumentException("An originating operation is required.", nameof(originatingOperationId));
            if (feedRevision <= 0) throw new ArgumentOutOfRangeException(nameof(feedRevision));
            if (recordedAtMilliseconds < 0 || recordedAtMilliseconds > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(recordedAtMilliseconds));
            RewardValidation.BusinessSource(businessSource, nameof(businessSource));
            RewardValidation.Text(planId, 128, nameof(planId));
            if (planVersion <= 0) throw new ArgumentOutOfRangeException(nameof(planVersion));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            this.lines = lines.ToArray();
            if (this.lines.Length == 0 || this.lines.Length > 64 || this.lines.Any(value => value == null) ||
                this.lines.Select(value => value.LineIndex).Distinct().Count() != this.lines.Length ||
                this.lines.Select(value => value.LineIndex).OrderBy(value => value).Where((value, index) => value != index).Any())
                throw new ArgumentException("Receipt lines must be complete, bounded, and ordered by their zero-based line index.", nameof(lines));
            GrantId = grantId; OriginatingOperationId = originatingOperationId; FeedRevision = feedRevision;
            RecordedAtMilliseconds = recordedAtMilliseconds; BusinessSource = businessSource; PlanId = planId; PlanVersion = planVersion;
        }
        public Guid GrantId { get; }
        public OperationId OriginatingOperationId { get; }
        public long FeedRevision { get; }
        public long RecordedAtMilliseconds { get; }
        public string BusinessSource { get; }
        public string PlanId { get; }
        public int PlanVersion { get; }
        public IReadOnlyList<RewardReceiptLine> Lines => Array.AsReadOnly(lines);
    }

    /// <summary>Caller identity for an observation of a trusted receipt endpoint. The receipt can originate from an earlier idempotent operation.</summary>
    public sealed class RewardReceiptObservation
    {
        public RewardReceiptObservation(ScopedOwnerContext owner, OperationId operationId, RewardReceipt receipt)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("An operation identity is required.", nameof(operationId));
            Owner = owner; OperationId = operationId; Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public RewardReceipt Receipt { get; }
    }

    /// <summary>Receipt-group membership observed from the ordered private feed after the Wallet, Inventory and Entitlements projections were installed.</summary>
    public sealed class RewardProjectionLine
    {
        public RewardProjectionLine(int lineIndex, RewardReceiptLineKind kind, PlatformId resourceId, long projectionRevision)
        {
            if (lineIndex < 0 || lineIndex > 63) throw new ArgumentOutOfRangeException(nameof(lineIndex));
            if (!Enum.IsDefined(typeof(RewardReceiptLineKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!resourceId.IsValid) throw new ArgumentException("A projection resource identity is required.", nameof(resourceId));
            if (projectionRevision < 0) throw new ArgumentOutOfRangeException(nameof(projectionRevision));
            LineIndex = lineIndex; Kind = kind; ResourceId = resourceId; ProjectionRevision = projectionRevision;
        }
        public int LineIndex { get; }
        public RewardReceiptLineKind Kind { get; }
        public PlatformId ResourceId { get; }
        public long ProjectionRevision { get; }
    }

    /// <summary>Feed evidence only. It carries no balances, holdings, right payloads or local mutation authority.</summary>
    public sealed class RewardProjectionGroup
    {
        private readonly RewardProjectionLine[] lines;
        public RewardProjectionGroup(ScopedOwnerContext owner, OperationId operationId, Guid grantId, string businessSource,
            long feedRevision, IEnumerable<RewardProjectionLine> lines)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("An operation identity is required.", nameof(operationId));
            if (grantId == Guid.Empty) throw new ArgumentException("A grant identity is required.", nameof(grantId));
            RewardValidation.BusinessSource(businessSource, nameof(businessSource));
            if (feedRevision <= 0) throw new ArgumentOutOfRangeException(nameof(feedRevision));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            this.lines = lines.ToArray();
            if (this.lines.Length == 0 || this.lines.Length > 64 || this.lines.Any(value => value == null) || this.lines.Select(value => value.LineIndex).Distinct().Count() != this.lines.Length)
                throw new ArgumentException("Projection-group lines must be bounded and distinct.", nameof(lines));
            Owner = owner; OperationId = operationId; GrantId = grantId; BusinessSource = businessSource; FeedRevision = feedRevision;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public Guid GrantId { get; }
        public string BusinessSource { get; }
        public long FeedRevision { get; }
        public IReadOnlyList<RewardProjectionLine> Lines => Array.AsReadOnly(lines);
    }

    public sealed class RewardRejection
    {
        public RewardRejection(ScopedOwnerContext owner, OperationId operationId, string businessSource, string code)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("An operation identity is required.", nameof(operationId));
            RewardValidation.BusinessSource(businessSource, nameof(businessSource)); RewardValidation.Text(code, 128, nameof(code));
            Owner = owner; OperationId = operationId; BusinessSource = businessSource; Code = code;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public string BusinessSource { get; }
        public string Code { get; }
    }

    public sealed class RewardPresentationRecord
    {
        public RewardPresentationRecord(OperationId operationId, string businessSource, RewardPresentationStatus status, RewardReceipt? receipt, string? rejectionCode, bool presented)
        {
            if (!operationId.IsValid) throw new ArgumentException("An operation identity is required.", nameof(operationId));
            RewardValidation.BusinessSource(businessSource, nameof(businessSource));
            if (!Enum.IsDefined(typeof(RewardPresentationStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if ((status == RewardPresentationStatus.AcceptedAwaitingPull || status == RewardPresentationStatus.Confirmed) != (receipt != null))
                throw new ArgumentException("Accepted and confirmed records require a receipt; pending and rejected records do not.", nameof(receipt));
            if ((status == RewardPresentationStatus.Rejected) != (rejectionCode != null)) throw new ArgumentException("Only a rejected record carries a rejection code.", nameof(rejectionCode));
            if (rejectionCode != null) RewardValidation.Text(rejectionCode, 128, nameof(rejectionCode));
            if (presented && status != RewardPresentationStatus.Confirmed) throw new ArgumentException("Only confirmed receipts can be presented.", nameof(presented));
            OperationId = operationId; BusinessSource = businessSource; Status = status; Receipt = receipt; RejectionCode = rejectionCode; Presented = presented;
        }
        public OperationId OperationId { get; }
        public string BusinessSource { get; }
        public RewardPresentationStatus Status { get; }
        public RewardReceipt? Receipt { get; }
        public string? RejectionCode { get; }
        public bool Presented { get; }
    }

    public sealed class RewardPresentationQuery
    {
        public RewardPresentationQuery(ScopedOwnerContext owner, OperationId operationId)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("An operation identity is required.", nameof(operationId));
            Owner = owner; OperationId = operationId;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
    }

    public interface IRewardFulfillmentStateCodec
    {
        byte[] EncodeRecord(RewardPresentationRecord record);
        RewardPresentationRecord DecodeRecord(long revision, ReadOnlySpan<byte> payload);
        byte[] EncodeGroup(RewardProjectionGroup group);
        RewardProjectionGroup DecodeGroup(ScopedOwnerContext owner, long revision, ReadOnlySpan<byte> payload);
    }

    public sealed class RewardFulfillmentOwnerMismatchException : InvalidOperationException { public RewardFulfillmentOwnerMismatchException() : base("Reward receipt data does not belong to the captured account/view generation.") { } }
    public sealed class RewardFulfillmentConflictException : InvalidOperationException { public RewardFulfillmentConflictException(string reason) : base(reason) { } }

    internal static class RewardValidation
    {
        private static readonly Regex Source = new Regex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,255}$", RegexOptions.CultureInvariant);
        internal static void BusinessSource(string value, string parameter) { if (string.IsNullOrWhiteSpace(value) || !Source.IsMatch(value)) throw new ArgumentOutOfRangeException(parameter); }
        internal static void Text(string value, int maximum, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
