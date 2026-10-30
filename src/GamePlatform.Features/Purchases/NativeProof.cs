using System;
namespace GamePlatform.Features.Purchases
{
    /// <summary>Private native proof. Never include in presentation, diagnostics, or telemetry.</summary>
    public sealed class NativeProof
    {
        public NativeProof(string packageName, string storeProductId, string token)
        {
            PackageName = packageName ?? throw new ArgumentNullException(nameof(packageName));
            StoreProductId = storeProductId ?? throw new ArgumentNullException(nameof(storeProductId));
            Token = token ?? throw new ArgumentNullException(nameof(token));
            if (new System.Text.UTF8Encoding(false, true).GetByteCount(token) is < 1 or > 8192)
                throw new ArgumentOutOfRangeException(nameof(token));
        }
        public string PackageName { get; }
        public string StoreProductId { get; }
        public string Token { get; }
    }
}
