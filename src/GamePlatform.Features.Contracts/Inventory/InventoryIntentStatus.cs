using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{
    public enum InventoryIntentStatus
    {
        AwaitingReceipt = 0, AcceptedAwaitingPull = 1
    }
}
