#nullable enable
using System;
using System.Collections.Generic;

namespace GamePlatform.Wire.Contracts
{
    public sealed class StoreOfferPurchasedResult : IPushAcceptedResult
    {
        public StoreOfferPurchasedResult(Guid purchaseId, Guid originatingOperationId, string offerId, int offerVersion, Guid purchaseKey,
            StoreOfferDebit debit, IReadOnlyList<ILiveSliceRewardLine> lines)
        {
            PurchaseId = purchaseId;
            OriginatingOperationId = originatingOperationId;
            OfferId = offerId ?? throw new ArgumentNullException(nameof(offerId));
            OfferVersion = offerVersion;
            PurchaseKey = purchaseKey;
            Debit = debit ?? throw new ArgumentNullException(nameof(debit));
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        }

        public Guid PurchaseId { get; }
        public Guid OriginatingOperationId { get; }
        public string OfferId { get; }
        public int OfferVersion { get; }
        public Guid PurchaseKey { get; }
        public StoreOfferDebit Debit { get; }
        public IReadOnlyList<ILiveSliceRewardLine> Lines { get; }
    }
}
