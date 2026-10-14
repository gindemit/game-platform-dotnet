using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Entitlements
{
    /// <summary>Server-confirmed right state. Active is not itself offline proof when an expiry exists.</summary>
    public enum EntitlementRightState
    {
        Active = 0, Revoked = 1, Expired = 2, Unknown = 3
    }
}
