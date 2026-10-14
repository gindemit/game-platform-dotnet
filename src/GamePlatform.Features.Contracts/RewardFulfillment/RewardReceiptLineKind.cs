using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{
    public enum RewardReceiptLineKind
    {
        Currency = 0, ItemStack = 1, Entitlement = 2
    }
}
