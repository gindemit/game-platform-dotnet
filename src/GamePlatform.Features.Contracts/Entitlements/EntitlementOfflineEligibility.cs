using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Entitlements
{

    /// <summary>What a client may honestly present from a cached projection. It never grants a right.</summary>
    public enum EntitlementOfflineEligibility
    {
        Eligible = 0, Ineligible = 1, ExpiryUncertain = 2, Unknown = 3
    }
}
