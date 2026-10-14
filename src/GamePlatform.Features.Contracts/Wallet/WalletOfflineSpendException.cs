using System;

namespace GamePlatform.Features.Contracts.Wallet
{
    public sealed class WalletOfflineSpendException : InvalidOperationException
    {
        public WalletOfflineSpendException() : base("A cached wallet balance cannot authorize a spend.") { }
    }
}
