using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{

    public sealed class InventoryOwnerMismatchException : InvalidOperationException
    {
        public InventoryOwnerMismatchException() : base("Inventory data does not belong to the captured account/view generation.") { }
    }
}
