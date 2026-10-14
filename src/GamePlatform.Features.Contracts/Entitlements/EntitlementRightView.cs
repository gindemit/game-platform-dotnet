using System;

namespace GamePlatform.Features.Contracts.Entitlements
{

    public sealed class EntitlementRightView
    {
        public EntitlementRightView(EntitlementRight right, EntitlementOfflineEligibility offlineEligibility)
        {
            Right = right ?? throw new ArgumentNullException(nameof(right));
            if (!Enum.IsDefined(typeof(EntitlementOfflineEligibility), offlineEligibility))
                throw new ArgumentOutOfRangeException(nameof(offlineEligibility));
            OfflineEligibility = offlineEligibility;
        }

        public EntitlementRight Right { get; }
        public EntitlementOfflineEligibility OfflineEligibility { get; }
    }
}
