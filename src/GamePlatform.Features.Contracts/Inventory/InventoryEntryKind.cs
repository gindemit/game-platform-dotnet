using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{
    /// <summary>Server-confirmed inventory forms. Stack quantities and instances never share an identity.</summary>
    public enum InventoryEntryKind
    {
        Stack = 0, Instance = 1
    }
}
