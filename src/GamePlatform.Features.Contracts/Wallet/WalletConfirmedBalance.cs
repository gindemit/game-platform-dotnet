using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    /// <summary>One server-confirmed exact signed-64 balance. Local code never derives a replacement balance.</summary>
    public sealed class WalletConfirmedBalance
    {
        private readonly byte[] extensions;
        public WalletConfirmedBalance(WalletCurrency currency, long balance, long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> extensions)
        {
            Currency = currency ?? throw new ArgumentNullException(nameof(currency));
            if (revision <= 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            WalletValidation.Timestamp(confirmedAtMilliseconds, nameof(confirmedAtMilliseconds));
            if (extensions.Length > WalletValidation.MaximumExtensionBytes)
                throw new ArgumentOutOfRangeException(nameof(extensions));
            Balance = balance;
            Revision = revision;
            ConfirmedAtMilliseconds = confirmedAtMilliseconds;
            this.extensions = extensions.ToArray();
        }
        public WalletCurrency Currency { get; }
        public long Balance { get; }
        public long Revision { get; }
        public long ConfirmedAtMilliseconds { get; }
        public byte[] CopyExtensions() => (byte[])extensions.Clone();
    }
}
