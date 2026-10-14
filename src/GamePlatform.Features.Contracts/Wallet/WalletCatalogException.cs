using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{
    public sealed class WalletCatalogException : InvalidOperationException
    {
        public WalletCatalogException(string message) : base(message) { }
    }
}
