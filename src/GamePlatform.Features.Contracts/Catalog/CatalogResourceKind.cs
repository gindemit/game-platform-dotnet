using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Catalog
{
    /// <summary>Server-owned resource kinds supported by the bounded value slice.</summary>
    public enum CatalogResourceKind
    {
        Currency, Item, Entitlement, Reward
    }
}
