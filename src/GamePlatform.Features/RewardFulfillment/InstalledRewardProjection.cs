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
        public InstalledRewardProjection(RewardReceiptLineKind kind, PlatformId resourceId, long projectionRevision)
        {
            if (!Enum.IsDefined(typeof(RewardReceiptLineKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!resourceId.IsValid)
                throw new ArgumentException("A resource is required.", nameof(resourceId));
            if (projectionRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(projectionRevision));
            Kind = kind;
            ResourceId = resourceId;
            ProjectionRevision = projectionRevision;
        }
        public RewardReceiptLineKind Kind { get; }
        public PlatformId ResourceId { get; }
        public long ProjectionRevision { get; }
    }
}
