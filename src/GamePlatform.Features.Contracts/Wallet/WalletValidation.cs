using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    internal static class WalletValidation
    {
        internal const int MaximumExtensionBytes = 16_384;
        private static readonly Regex Code = new Regex("^[a-z][a-z0-9_.:-]{0,127}$", RegexOptions.CultureInvariant);
        internal static void Timestamp(long value, string parameter)
        {
            if (value < 0 || value > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(parameter);
        }
        internal static void BusinessSource(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentOutOfRangeException(parameter);
        }
        internal static void Diagnostic(string? value, string parameter)
        {
            if (value == null || !Code.IsMatch(value))
                throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
