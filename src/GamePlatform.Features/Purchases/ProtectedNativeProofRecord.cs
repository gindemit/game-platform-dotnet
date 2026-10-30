using System;
namespace GamePlatform.Features.Purchases
{
    public sealed class ProtectedNativeProofRecord
    {
        public ProtectedNativeProofRecord(Guid proofId, Guid recoveryIntentKey, string packageName, string storeProductId, Guid? reservedAttemptKey = null, Guid? finalizedChargeId = null)
        {
            if (finalizedChargeId.HasValue)
            {
                if (!IsV7(finalizedChargeId.Value) || !IsV7(proofId) || !IsV7(recoveryIntentKey) ||
                    (reservedAttemptKey.HasValue && !IsV7(reservedAttemptKey.Value)) ||
                    string.IsNullOrWhiteSpace(packageName) || packageName.Length > 255 || string.IsNullOrWhiteSpace(storeProductId) || storeProductId.Length > 255)
                    throw new ArgumentException("A finalized proof requires its immutable UUIDv7 identities and product scope.", nameof(finalizedChargeId));
            }
            ProofId = proofId;
            RecoveryIntentKey = recoveryIntentKey;
            PackageName = packageName;
            StoreProductId = storeProductId;
            ReservedAttemptKey = reservedAttemptKey;
            FinalizedChargeId = finalizedChargeId;
        }
        public Guid ProofId { get; }
        public Guid RecoveryIntentKey { get; }
        public string PackageName { get; }
        public string StoreProductId { get; }
        public Guid? ReservedAttemptKey { get; }
        public Guid? FinalizedChargeId { get; }
        private static bool IsV7(Guid id)
        {
            string value = id.ToString("D");
            return id != Guid.Empty && value[14] == '7' && "89ab".IndexOf(value[19]) >= 0;
        }
    }
}
