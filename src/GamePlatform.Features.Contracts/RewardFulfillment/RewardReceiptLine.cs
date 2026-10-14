using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{

    /// <summary>One immutable line from the bounded, trusted reward-receipt response. This is receipt evidence, not a client grant plan.</summary>
    public sealed class RewardReceiptLine
    {
        public RewardReceiptLine(int lineIndex, int definitionVersion, RewardReceiptLineKind kind, PlatformId resourceId, long? quantity, long? expiresAtServerMilliseconds)
        {
            if (lineIndex < 0 || lineIndex > 63)
                throw new ArgumentOutOfRangeException(nameof(lineIndex));
            if (definitionVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(definitionVersion));
            if (!Enum.IsDefined(typeof(RewardReceiptLineKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!resourceId.IsValid)
                throw new ArgumentException("A receipt resource identity is required.", nameof(resourceId));
            if (kind == RewardReceiptLineKind.Entitlement)
            {
                if (quantity.HasValue)
                    throw new ArgumentException("Entitlement receipt lines carry no quantity.", nameof(quantity));
            }
            else if (!quantity.HasValue || quantity.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            if (expiresAtServerMilliseconds.HasValue && (expiresAtServerMilliseconds.Value < 0 || expiresAtServerMilliseconds.Value > 253_402_300_799_999L))
                throw new ArgumentOutOfRangeException(nameof(expiresAtServerMilliseconds));
            if (kind != RewardReceiptLineKind.Entitlement && expiresAtServerMilliseconds.HasValue)
                throw new ArgumentException("Only entitlement lines carry an expiry.", nameof(expiresAtServerMilliseconds));
            LineIndex = lineIndex;
            DefinitionVersion = definitionVersion;
            Kind = kind;
            ResourceId = resourceId;
            Quantity = quantity;
            ExpiresAtServerMilliseconds = expiresAtServerMilliseconds;
        }
        public int LineIndex { get; }
        public int DefinitionVersion { get; }
        public RewardReceiptLineKind Kind { get; }
        public PlatformId ResourceId { get; }
        public long? Quantity { get; }
        public long? ExpiresAtServerMilliseconds { get; }
    }
}
