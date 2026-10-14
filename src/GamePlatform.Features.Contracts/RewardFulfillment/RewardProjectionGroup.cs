using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{

    /// <summary>Feed evidence only. It carries no balances, holdings, right payloads or local mutation authority.</summary>
    public sealed class RewardProjectionGroup
    {
        private readonly RewardProjectionLine[] lines;
        public RewardProjectionGroup(ScopedOwnerContext owner, OperationId operationId, Guid grantId, string businessSource,
            long feedRevision, IEnumerable<RewardProjectionLine> lines)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("An operation identity is required.", nameof(operationId));
            if (grantId == Guid.Empty)
                throw new ArgumentException("A grant identity is required.", nameof(grantId));
            RewardValidation.BusinessSource(businessSource, nameof(businessSource));
            if (feedRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(feedRevision));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));
            this.lines = lines.ToArray();
            if (this.lines.Length == 0 || this.lines.Length > 64 || this.lines.Any(value => value == null) || this.lines.Select(value => value.LineIndex).Distinct().Count() != this.lines.Length)
                throw new ArgumentException("Projection-group lines must be bounded and distinct.", nameof(lines));
            Owner = owner;
            OperationId = operationId;
            GrantId = grantId;
            BusinessSource = businessSource;
            FeedRevision = feedRevision;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public Guid GrantId { get; }
        public string BusinessSource { get; }
        public long FeedRevision { get; }
        public IReadOnlyList<RewardProjectionLine> Lines => Array.AsReadOnly(lines);
    }
}
