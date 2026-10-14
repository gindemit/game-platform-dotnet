using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{
    public sealed class RewardFulfillmentConflictException : InvalidOperationException
    {
        public RewardFulfillmentConflictException(string reason) : base(reason) { }
    }
}
