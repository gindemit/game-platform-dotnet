using System;

namespace GamePlatform.Features.Contracts.Entitlements
{

    public sealed class EntitlementOwnerMismatchException : InvalidOperationException
    {
        public EntitlementOwnerMismatchException() : base("Entitlement data does not belong to the captured account/view generation.") { }
    }
}
