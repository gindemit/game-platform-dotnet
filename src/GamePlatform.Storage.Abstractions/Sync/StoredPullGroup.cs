using System;
using System.Collections.Generic;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public sealed class StoredPullGroup
    {
        public StoredPullGroup(long revision, IReadOnlyList<StoredProjectionMutation> changes)
        {
            Revision = revision;
            Changes = Copy(changes);
        }
        public long Revision { get; }
        public IReadOnlyList<StoredProjectionMutation> Changes { get; }
        private static IReadOnlyList<StoredProjectionMutation> Copy(IReadOnlyList<StoredProjectionMutation> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            var copy = new StoredProjectionMutation[values.Count];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = values[i] ?? throw new ArgumentException("A change is null.", nameof(values));
            return Array.AsReadOnly(copy);
        }
    }
}
