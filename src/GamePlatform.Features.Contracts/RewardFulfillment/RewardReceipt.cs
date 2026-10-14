using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{

    /// <summary>Immutable accepted backend receipt. Its source is the canonical backend business-source key, never a client-selected eligibility key.</summary>
    public sealed class RewardReceipt
    {
        private readonly RewardReceiptLine[] lines;
        public RewardReceipt(Guid grantId, OperationId originatingOperationId, long feedRevision, long recordedAtMilliseconds,
            string businessSource, string planId, int planVersion, IEnumerable<RewardReceiptLine> lines)
        {
            if (grantId == Guid.Empty)
                throw new ArgumentException("A grant identity is required.", nameof(grantId));
            if (!originatingOperationId.IsValid)
                throw new ArgumentException("An originating operation is required.", nameof(originatingOperationId));
            if (feedRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(feedRevision));
            if (recordedAtMilliseconds < 0 || recordedAtMilliseconds > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(recordedAtMilliseconds));
            RewardValidation.BusinessSource(businessSource, nameof(businessSource));
            RewardValidation.Text(planId, 128, nameof(planId));
            if (planVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(planVersion));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));
            this.lines = lines.ToArray();
            if (this.lines.Length == 0 || this.lines.Length > 64 || this.lines.Any(value => value == null) ||
                this.lines.Select(value => value.LineIndex).Distinct().Count() != this.lines.Length ||
                this.lines.Select(value => value.LineIndex).OrderBy(value => value).Where((value, index) => value != index).Any())
                throw new ArgumentException("Receipt lines must be complete, bounded, and ordered by their zero-based line index.", nameof(lines));
            GrantId = grantId;
            OriginatingOperationId = originatingOperationId;
            FeedRevision = feedRevision;
            RecordedAtMilliseconds = recordedAtMilliseconds;
            BusinessSource = businessSource;
            PlanId = planId;
            PlanVersion = planVersion;
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
}
