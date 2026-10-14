using System;

namespace GamePlatform.Features.Wallet
{

    public sealed class G3WalletProjectionOwnerMismatchException : InvalidOperationException
    {
        public G3WalletProjectionOwnerMismatchException() : base("The G3 wallet projection does not belong to the captured account/view generation.") { }
    }
}
