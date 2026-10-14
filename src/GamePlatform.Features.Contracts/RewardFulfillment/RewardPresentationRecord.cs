using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{

    public sealed class RewardPresentationRecord
    {
        public RewardPresentationRecord(OperationId operationId, string businessSource, RewardPresentationStatus status, RewardReceipt? receipt, string? rejectionCode, bool presented)
        {
            if (!operationId.IsValid)
                throw new ArgumentException("An operation identity is required.", nameof(operationId));
            RewardValidation.BusinessSource(businessSource, nameof(businessSource));
            if (!Enum.IsDefined(typeof(RewardPresentationStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if ((status == RewardPresentationStatus.AcceptedAwaitingPull || status == RewardPresentationStatus.Confirmed) != (receipt != null))
                throw new ArgumentException("Accepted and confirmed records require a receipt; pending and rejected records do not.", nameof(receipt));
            if ((status == RewardPresentationStatus.Rejected) != (rejectionCode != null))
                throw new ArgumentException("Only a rejected record carries a rejection code.", nameof(rejectionCode));
            if (rejectionCode != null)
                RewardValidation.Text(rejectionCode, 128, nameof(rejectionCode));
            if (presented && status != RewardPresentationStatus.Confirmed)
                throw new ArgumentException("Only confirmed receipts can be presented.", nameof(presented));
            OperationId = operationId;
            BusinessSource = businessSource;
            Status = status;
            Receipt = receipt;
            RejectionCode = rejectionCode;
            Presented = presented;
        }
        public OperationId OperationId { get; }
        public string BusinessSource { get; }
        public RewardPresentationStatus Status { get; }
        public RewardReceipt? Receipt { get; }
        public string? RejectionCode { get; }
        public bool Presented { get; }
    }
}
