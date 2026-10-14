using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{

    /// <summary>Frozen semantic authority boundary; it does not create a provider route or make an offline holding spendable.</summary>
    public interface IInventoryIntentAuthority
    {
        InventoryIntentAuthorization Authorize(InventoryIntentRequest request);
    }
}
