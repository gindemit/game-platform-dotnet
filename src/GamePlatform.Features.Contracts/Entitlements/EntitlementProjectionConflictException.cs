using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Entitlements
{

    public sealed class EntitlementProjectionConflictException : InvalidOperationException
    {
        public EntitlementProjectionConflictException(string reason) : base(reason) { }
    }
}
