using System;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Entitlements
{

    /// <summary>Non-secret receipt identity retained only as the server-confirmed origin of a right.</summary>
    public sealed class EntitlementOriginReceipt
    {
        private static readonly Regex Source = new Regex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,255}$", RegexOptions.CultureInvariant);

        public EntitlementOriginReceipt(PlatformId receiptId, string businessSource)
        {
            if (!receiptId.IsValid)
                throw new ArgumentException("A receipt identity is required.", nameof(receiptId));
            if (string.IsNullOrWhiteSpace(businessSource) || !Source.IsMatch(businessSource))
                throw new ArgumentOutOfRangeException(nameof(businessSource));
            ReceiptId = receiptId;
            BusinessSource = businessSource;
        }

        public PlatformId ReceiptId { get; }
        public string BusinessSource { get; }
    }
}
