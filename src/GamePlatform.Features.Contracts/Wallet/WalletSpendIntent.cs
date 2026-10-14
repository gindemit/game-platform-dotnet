using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    /// <summary>Immutable client request to ask the backend to spend a positive exact amount.</summary>
    public sealed class WalletSpendIntent
    {
        public WalletSpendIntent(WalletCurrency currency, long expectedRevision, long amount)
        {
            Currency = currency ?? throw new ArgumentNullException(nameof(currency));
            if (expectedRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            ExpectedRevision = expectedRevision;
            Amount = amount;
        }
        public WalletCurrency Currency { get; }
        public long ExpectedRevision { get; }
        public long Amount { get; }
    }
}
