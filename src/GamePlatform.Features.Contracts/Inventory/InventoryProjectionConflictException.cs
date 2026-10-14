using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{
    public sealed class InventoryProjectionConflictException : InvalidOperationException
    {
        public InventoryProjectionConflictException(string reason) : base(reason) { }
    }
}
