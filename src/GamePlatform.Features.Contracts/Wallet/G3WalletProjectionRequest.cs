using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    /// <summary>Exact owner/view/generation fenced cached-wallet read key for the G3 projection.</summary>
    public sealed class G3WalletProjectionRequest
    {
        public G3WalletProjectionRequest(ScopedOwnerContext owner, SemanticId currencyId)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!currencyId.IsValid)
                throw new ArgumentException("A semantic currency ID is required.", nameof(currencyId));
            Owner = owner;
            CurrencyId = currencyId;
        }

        public ScopedOwnerContext Owner { get; }
        public SemanticId CurrencyId { get; }
    }
}
