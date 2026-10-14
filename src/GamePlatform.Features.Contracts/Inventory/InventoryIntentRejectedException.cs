using System;

namespace GamePlatform.Features.Contracts.Inventory
{
    public sealed class InventoryIntentRejectedException : InvalidOperationException
    {
        public InventoryIntentRejectedException(string reason) : base(reason) { }
    }
}
