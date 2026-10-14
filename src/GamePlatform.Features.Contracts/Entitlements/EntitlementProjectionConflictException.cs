using System;

namespace GamePlatform.Features.Contracts.Entitlements
{

    public sealed class EntitlementProjectionConflictException : InvalidOperationException
    {
        public EntitlementProjectionConflictException(string reason) : base(reason) { }
    }
}
