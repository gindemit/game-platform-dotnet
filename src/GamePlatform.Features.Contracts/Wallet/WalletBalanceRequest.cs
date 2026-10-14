using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    public sealed class WalletBalanceRequest
    {
        public WalletBalanceRequest(ScopedOwnerContext owner, WalletCurrency currency)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            Owner = owner;
            Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        }
        public ScopedOwnerContext Owner { get; }
        public WalletCurrency Currency { get; }
    }
}
