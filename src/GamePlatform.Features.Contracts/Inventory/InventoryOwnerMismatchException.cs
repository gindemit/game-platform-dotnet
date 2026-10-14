using System;

namespace GamePlatform.Features.Contracts.Inventory
{

    public sealed class InventoryOwnerMismatchException : InvalidOperationException
    {
        public InventoryOwnerMismatchException() : base("Inventory data does not belong to the captured account/view generation.") { }
    }
}
