using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{
    public sealed class WalletOfflineSpendException : InvalidOperationException
    {
        public WalletOfflineSpendException() : base("A cached wallet balance cannot authorize a spend.") { }
    }
}
