using System;
namespace GamePlatform.Features.Purchases
{
    public sealed class NativeLaunchRequest
    {
        public NativeLaunchRequest(string packageName, string storeProductId, Guid customerBinding, NativeStoreProduct product, Guid intentKey)
        { PackageName = packageName; StoreProductId = storeProductId; CustomerBinding = customerBinding; Product = product ?? throw new ArgumentNullException(nameof(product)); IntentKey = intentKey; }
        public string PackageName { get; }
        public string StoreProductId { get; }
        public Guid CustomerBinding { get; }
        public NativeStoreProduct Product { get; }
        public Guid IntentKey { get; }
    }
}
