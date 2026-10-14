using System;

namespace GamePlatform.Features.Contracts.Wallet
{
    public sealed class WalletCatalogException : InvalidOperationException
    {
        public WalletCatalogException(string message) : base(message) { }
    }
}
