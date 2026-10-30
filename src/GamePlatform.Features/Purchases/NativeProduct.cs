using System;
using System.Collections.Generic;
using System.Linq;
namespace GamePlatform.Features.Purchases
{
    public sealed class NativeProduct
    {
        public NativeProduct(string productId, int version, string packageName, string storeProductId, string environment,
            IEnumerable<NativeContentLine>? contents = null, string productType = "consumable")
        {
            ProductId = productId ?? throw new ArgumentNullException(nameof(productId));
            Version = version;
            PackageName = packageName ?? throw new ArgumentNullException(nameof(packageName));
            StoreProductId = storeProductId ?? throw new ArgumentNullException(nameof(storeProductId));
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            if (productType != "consumable" && productType != "non_consumable")
                throw new ArgumentException("Unsupported native product type.", nameof(productType));
            ProductType = productType;
            Contents = Array.AsReadOnly((contents ?? Array.Empty<NativeContentLine>()).ToArray());
            if (version <= 0)
                throw new ArgumentOutOfRangeException(nameof(version));
        }
        public string ProductId { get; }
        public int Version { get; }
        public string PackageName { get; }
        public string StoreProductId { get; }
        public string Environment { get; }
        public string ProductType { get; }
        public IReadOnlyList<NativeContentLine> Contents { get; }
    }
}
