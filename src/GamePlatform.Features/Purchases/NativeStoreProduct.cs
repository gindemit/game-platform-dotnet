namespace GamePlatform.Features.Purchases
{
    public sealed class NativeStoreProduct
    {
        public NativeStoreProduct(string packageName, string storeProductId, string displayPrice)
        { PackageName = packageName; StoreProductId = storeProductId; DisplayPrice = displayPrice; }
        public string PackageName { get; }
        public string StoreProductId { get; }
        public string DisplayPrice { get; }
    }
}
