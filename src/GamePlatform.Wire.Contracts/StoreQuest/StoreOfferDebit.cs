#nullable enable
using System;

namespace GamePlatform.Wire.Contracts
{
    public sealed class StoreOfferDebit
    {
        public StoreOfferDebit(string currencyId, long quantity)
        {
            CurrencyId = currencyId ?? throw new ArgumentNullException(nameof(currencyId));
            Quantity = quantity;
        }

        public string CurrencyId { get; }
        public long Quantity { get; }
    }
}
