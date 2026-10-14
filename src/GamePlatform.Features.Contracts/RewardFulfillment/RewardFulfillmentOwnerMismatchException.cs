using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{

    public sealed class RewardFulfillmentOwnerMismatchException : InvalidOperationException
    {
        public RewardFulfillmentOwnerMismatchException() : base("Reward receipt data does not belong to the captured account/view generation.") { }
    }
}
