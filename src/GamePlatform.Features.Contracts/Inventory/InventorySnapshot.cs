using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlatform.Features.Contracts.Inventory
{

    public sealed class InventorySnapshot
    {
        private readonly InventoryIntent[] pending;
        public InventorySnapshot(InventoryConfirmedSnapshot? confirmed, IEnumerable<InventoryIntent> pending)
        {
            Confirmed = confirmed;
            if (pending == null)
                throw new ArgumentNullException(nameof(pending));
            this.pending = pending.ToArray();
            if (this.pending.Length > 64 || this.pending.Any(value => value == null) || this.pending.Select(value => value.OperationId).Distinct().Count() != this.pending.Length)
                throw new ArgumentException("Pending inventory intents are invalid.", nameof(pending));
        }
        public InventoryConfirmedSnapshot? Confirmed { get; }
        public IReadOnlyList<InventoryIntent> PendingIntents => Array.AsReadOnly(pending);
    }
}
