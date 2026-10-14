using System;

namespace GamePlatform.Features.Contracts.Wallet
{

    public sealed class WalletConflictException : InvalidOperationException
    {
        public WalletConflictException(string message) : base(message) { }
    }
}
