using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{

    public sealed class InventoryStack
    {
        public InventoryStack(PlatformId definitionId, long quantity, long revision)
        {
            if (!definitionId.IsValid)
                throw new ArgumentException("A definition ID is required.", nameof(definitionId));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            DefinitionId = definitionId;
            Quantity = quantity;
            Revision = revision;
        }
        public PlatformId DefinitionId { get; }
        public long Quantity { get; }
        public long Revision { get; }
    }
}
