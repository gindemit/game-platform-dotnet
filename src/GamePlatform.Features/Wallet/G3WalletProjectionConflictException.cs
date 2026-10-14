using System;

namespace GamePlatform.Features.Wallet
{

    public sealed class G3WalletProjectionConflictException : InvalidOperationException
    {
        public G3WalletProjectionConflictException(string message) : base(message) { }
    }
}
