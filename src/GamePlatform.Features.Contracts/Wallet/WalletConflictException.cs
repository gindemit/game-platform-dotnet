using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    public sealed class WalletConflictException : InvalidOperationException
    {
        public WalletConflictException(string message) : base(message) { }
    }
}
