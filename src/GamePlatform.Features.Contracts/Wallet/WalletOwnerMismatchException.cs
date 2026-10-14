using System;

namespace GamePlatform.Features.Contracts.Wallet
{
    public sealed class WalletOwnerMismatchException : InvalidOperationException
    {
        public WalletOwnerMismatchException() : base("The wallet result does not belong to the captured account/view generation.") { }
    }
}
