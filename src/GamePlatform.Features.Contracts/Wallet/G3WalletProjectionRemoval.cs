using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    /// <summary>Explicit private-feed removal semantics. Both kinds read as Missing and never synthesize a zero balance.</summary>
    public enum G3WalletProjectionRemoval
    {
        RemoveFromView = 0,
        Reset = 1
    }
}
