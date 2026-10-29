using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlatform.Features.Store
{
    public sealed class StorefrontOffer
    {
        public StorefrontOffer(string offerId, int offerVersion, string currencyId, long price,
            IEnumerable<StorefrontLine> contents, int? purchaseLimit, long purchasedCount, string availability)
        {
            OfferId = offerId ?? throw new ArgumentNullException(nameof(offerId));
            OfferVersion = offerVersion;
            CurrencyId = currencyId ?? throw new ArgumentNullException(nameof(currencyId));
            Price = price;
            Contents = Array.AsReadOnly((contents ?? throw new ArgumentNullException(nameof(contents))).ToArray());
            PurchaseLimit = purchaseLimit;
            PurchasedCount = purchasedCount;
            Availability = availability ?? throw new ArgumentNullException(nameof(availability));
        }
        public string OfferId { get; }
        public int OfferVersion { get; }
        public string CurrencyId { get; }
        public long Price { get; }
        public IReadOnlyList<StorefrontLine> Contents { get; }
        public int? PurchaseLimit { get; }
        public long PurchasedCount { get; }
        public string Availability { get; }
    }
}
