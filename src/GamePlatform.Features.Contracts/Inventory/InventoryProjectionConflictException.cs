using System;

namespace GamePlatform.Features.Contracts.Inventory
{
    public sealed class InventoryProjectionConflictException : InvalidOperationException
    {
        public InventoryProjectionConflictException(string reason) : base(reason) { }
    }
}
