using System;
using System.Collections.Generic;
using System.Linq;
namespace GamePlatform.Features.Purchases
{
    public sealed class NativeIntentState
    {
        public NativeIntentState(Guid intentId, Guid intentKey, string productId, int productVersion, string packageName,
            string storeProductId, string environment, Guid customerBinding, string source, bool launchAllowed,
            string state, string? reason, string finishState, long? feedRevision, long? visibilityGeneration, Guid? logEpoch, Guid? grantId, Guid? chargeId,
            IReadOnlyList<NativeContentLine>? receiptLines = null, long? originalFeedRevision = null,
            Guid? originatingIntentId = null, int? retryAfterMilliseconds = null, string productType = "consumable")
        {
            IntentId = intentId;
            IntentKey = intentKey;
            ProductId = productId;
            ProductVersion = productVersion;
            PackageName = packageName;
            StoreProductId = storeProductId;
            Environment = environment;
            CustomerBinding = customerBinding;
            Source = source;
            LaunchAllowed = launchAllowed;
            State = state;
            Reason = reason;
            FinishState = finishState;
            FeedRevision = feedRevision;
            VisibilityGeneration = visibilityGeneration;
            LogEpoch = logEpoch;
            GrantId = grantId;
            ChargeId = chargeId;
            ReceiptLines = Array.AsReadOnly((receiptLines ?? Array.Empty<NativeContentLine>()).ToArray());
            OriginalFeedRevision = originalFeedRevision;
            OriginatingIntentId = originatingIntentId;
            RetryAfterMilliseconds = retryAfterMilliseconds;
            if (productType != "consumable" && productType != "non_consumable")
                throw new ArgumentException("Unsupported native product type.", nameof(productType));
            ProductType = productType;
        }
        public Guid IntentId { get; }
        public Guid IntentKey { get; }
        public string ProductId { get; }
        public int ProductVersion { get; }
        public string PackageName { get; }
        public string StoreProductId { get; }
        public string Environment { get; }
        public Guid CustomerBinding { get; }
        public string Source { get; }
        public bool LaunchAllowed { get; }
        public string State { get; }
        public string? Reason { get; }
        public string FinishState { get; }
        public long? FeedRevision { get; }
        public long? VisibilityGeneration { get; }
        public Guid? LogEpoch { get; }
        public Guid? GrantId { get; }
        public Guid? ChargeId { get; }
        public IReadOnlyList<NativeContentLine> ReceiptLines { get; }
        public long? OriginalFeedRevision { get; }
        public Guid? OriginatingIntentId { get; }
        public int? RetryAfterMilliseconds { get; }
        public string ProductType { get; }
    }
}
