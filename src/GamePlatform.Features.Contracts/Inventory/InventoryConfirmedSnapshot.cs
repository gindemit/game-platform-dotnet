using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlatform.Features.Contracts.Inventory
{

    /// <summary>An immutable confirmed projection. Pending intents are deliberately not represented as holdings.</summary>
    public sealed class InventoryConfirmedSnapshot
    {
        private readonly InventoryStack[] stacks;
        private readonly InventoryInstance[] instances;
        public InventoryConfirmedSnapshot(long revision, long confirmedAtMilliseconds, IEnumerable<InventoryStack> stacks, IEnumerable<InventoryInstance> instances)
        {
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            if (confirmedAtMilliseconds < 0 || confirmedAtMilliseconds > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(confirmedAtMilliseconds));
            this.stacks = Copy(stacks);
            this.instances = Copy(instances);
            if (this.stacks.Length > 512 || this.instances.Length > 512)
                throw new ArgumentOutOfRangeException(nameof(stacks));
            if (this.stacks.Select(value => value.DefinitionId).Distinct().Count() != this.stacks.Length ||
                this.instances.Select(value => value.InstanceId).Distinct().Count() != this.instances.Length)
                throw new ArgumentException("Inventory projection contains duplicate identities.");
            Revision = revision;
            ConfirmedAtMilliseconds = confirmedAtMilliseconds;
        }
        public long Revision { get; }
        public long ConfirmedAtMilliseconds { get; }
        public IReadOnlyList<InventoryStack> Stacks => Array.AsReadOnly(stacks);
        public IReadOnlyList<InventoryInstance> Instances => Array.AsReadOnly(instances);
        private static T[] Copy<T>(IEnumerable<T> values) where T : class
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            var copy = values.ToArray();
            if (copy.Any(value => value == null))
                throw new ArgumentException("A projection entry is missing.", nameof(values));
            return copy;
        }
    }
}
