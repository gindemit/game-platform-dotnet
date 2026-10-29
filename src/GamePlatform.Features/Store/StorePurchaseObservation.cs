using System;
using GamePlatform.Core;
namespace GamePlatform.Features.Store
{
    public sealed class StorePurchaseObservation
    {
        public StorePurchaseObservation(OperationId operationId, Guid purchaseKey, string offerId, int offerVersion, string status, string? rejectionCode)
        {
            OperationId = operationId;
            PurchaseKey = purchaseKey;
            OfferId = offerId;
            OfferVersion = offerVersion;
            Status = status;
            RejectionCode = rejectionCode;
        }
        public OperationId OperationId { get; }
        public Guid PurchaseKey { get; }
        public string OfferId { get; }
        public int OfferVersion { get; }
        public string Status { get; }
        public string? RejectionCode { get; }
    }
}
