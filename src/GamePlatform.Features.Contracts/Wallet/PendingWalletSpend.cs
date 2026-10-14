using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    /// <summary>Durable intent state; it is intentionally separate from the confirmed balance.</summary>
    public sealed class PendingWalletSpend
    {
        public PendingWalletSpend(OperationId operationId, ClientStreamId streamId, string businessSource, WalletSpendIntent intent,
            long localRevision, WalletPendingStatus status, long? acceptedRevision, string? rejectionCode)
        {
            if (!operationId.IsValid)
                throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (!streamId.IsValid)
                throw new ArgumentException("A valid stream identity is required.", nameof(streamId));
            WalletValidation.BusinessSource(businessSource, nameof(businessSource));
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            if (localRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(localRevision));
            if (!Enum.IsDefined(typeof(WalletPendingStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (status == WalletPendingStatus.AcceptedAwaitingPull && (!acceptedRevision.HasValue || acceptedRevision.Value <= intent.ExpectedRevision))
                throw new ArgumentException("An accepted pending spend requires a newer confirmed revision.", nameof(acceptedRevision));
            if (status != WalletPendingStatus.AcceptedAwaitingPull && acceptedRevision.HasValue)
                throw new ArgumentException("Only an accepted pending spend carries an accepted revision.", nameof(acceptedRevision));
            if (status == WalletPendingStatus.Rejected)
                WalletValidation.Diagnostic(rejectionCode, nameof(rejectionCode));
            else if (rejectionCode != null)
                throw new ArgumentException("Only a rejected pending spend carries a rejection code.", nameof(rejectionCode));
            OperationId = operationId;
            StreamId = streamId;
            BusinessSource = businessSource;
            LocalRevision = localRevision;
            Status = status;
            AcceptedRevision = acceptedRevision;
            RejectionCode = rejectionCode;
        }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public WalletSpendIntent Intent { get; }
        public long LocalRevision { get; }
        public WalletPendingStatus Status { get; }
        public long? AcceptedRevision { get; }
        public string? RejectionCode { get; }
    }
}
