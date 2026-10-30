using System;
namespace GamePlatform.Features.Purchases
{
    public sealed class ProtectedNativeProofRecord
    {
        public ProtectedNativeProofRecord(Guid proofId, Guid recoveryIntentKey, string packageName, string storeProductId, Guid? reservedAttemptKey = null)
        { ProofId = proofId; RecoveryIntentKey = recoveryIntentKey; PackageName = packageName; StoreProductId = storeProductId; ReservedAttemptKey = reservedAttemptKey; }
        public Guid ProofId { get; }
        public Guid RecoveryIntentKey { get; }
        public string PackageName { get; }
        public string StoreProductId { get; }
        public Guid? ReservedAttemptKey { get; }
    }
}
