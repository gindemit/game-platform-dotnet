using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Entitlements
{

    public sealed class EntitlementOwnerMismatchException : InvalidOperationException
    {
        public EntitlementOwnerMismatchException() : base("Entitlement data does not belong to the captured account/view generation.") { }
    }
}
