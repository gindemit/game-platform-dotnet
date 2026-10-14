using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{

    /// <summary>Receipt outcome only. It has no holdings, quantities, or grant instructions.</summary>
    public sealed class InventoryIntentAcceptance
    {
        public InventoryIntentAcceptance(ScopedOwnerContext owner, OperationId operationId, long resultingInventoryRevision)
        {
            if (!owner.IsValid || !operationId.IsValid)
                throw new ArgumentException("A captured owner and operation ID are required.");
            if (resultingInventoryRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(resultingInventoryRevision));
            Owner = owner;
            OperationId = operationId;
            ResultingInventoryRevision = resultingInventoryRevision;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public long ResultingInventoryRevision { get; }
    }
}
