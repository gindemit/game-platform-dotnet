#nullable enable
using System;

namespace GamePlatform.Wire.Contracts
{
    public sealed class StoreOfferPurchaseCommand : IPushPayload
    {
        public StoreOfferPurchaseCommand(string offerId, int offerVersion, Guid purchaseKey)
        {
            OfferId = offerId ?? throw new ArgumentNullException(nameof(offerId));
            OfferVersion = offerVersion;
            PurchaseKey = purchaseKey;
        }

        public string OfferId { get; }
        public int OfferVersion { get; }
        public Guid PurchaseKey { get; }
    }
}
