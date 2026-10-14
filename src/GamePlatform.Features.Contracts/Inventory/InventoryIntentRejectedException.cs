using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{
    public sealed class InventoryIntentRejectedException : InvalidOperationException
    {
        public InventoryIntentRejectedException(string reason) : base(reason) { }
    }
}
