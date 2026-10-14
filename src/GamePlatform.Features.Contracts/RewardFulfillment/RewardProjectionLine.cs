using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{

    /// <summary>Receipt-group membership observed from the ordered private feed after the Wallet, Inventory and Entitlements projections were installed.</summary>
    public sealed class RewardProjectionLine
    {
        public RewardProjectionLine(int lineIndex, RewardReceiptLineKind kind, PlatformId resourceId, long projectionRevision)
        {
            if (lineIndex < 0 || lineIndex > 63)
                throw new ArgumentOutOfRangeException(nameof(lineIndex));
            if (!Enum.IsDefined(typeof(RewardReceiptLineKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!resourceId.IsValid)
                throw new ArgumentException("A projection resource identity is required.", nameof(resourceId));
            if (projectionRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(projectionRevision));
            LineIndex = lineIndex;
            Kind = kind;
            ResourceId = resourceId;
            ProjectionRevision = projectionRevision;
        }
        public int LineIndex { get; }
        public RewardReceiptLineKind Kind { get; }
        public PlatformId ResourceId { get; }
        public long ProjectionRevision { get; }
    }
}
