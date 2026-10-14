using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{
    /// <summary>
    /// Frozen G3 confirmed-wallet projection. Its semantic currency identity is display data only;
    /// it deliberately has no catalog definition, spend, credit, grant, or authority surface.
    /// </summary>
    public sealed class G3ConfirmedWalletProjection
    {
        public G3ConfirmedWalletProjection(SemanticId currencyId, long balance, long revision)
        {
            if (!currencyId.IsValid)
                throw new ArgumentException("A semantic currency ID is required.", nameof(currencyId));
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            CurrencyId = currencyId;
            Balance = balance;
            Revision = revision;
        }

        public SemanticId CurrencyId { get; }
        public long Balance { get; }
        public long Revision { get; }
    }
}
