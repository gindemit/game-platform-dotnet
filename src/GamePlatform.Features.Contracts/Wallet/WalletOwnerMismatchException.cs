using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{
    public sealed class WalletOwnerMismatchException : InvalidOperationException
    {
        public WalletOwnerMismatchException() : base("The wallet result does not belong to the captured account/view generation.") { }
    }
}
