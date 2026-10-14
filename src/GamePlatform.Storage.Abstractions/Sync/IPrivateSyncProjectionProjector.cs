using System.Collections.Generic;

namespace GamePlatform.Storage.Abstractions.Sync
{
    /// <summary>Projects confirmed private-feed data in the SQLite transaction that changed its raw rows.</summary>
    public interface IPrivateSyncProjectionProjector
    {
        /// <summary>The group is supplied in exact feed order and its revision remains its feed correlation.</summary>
        void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group);
        /// <summary>Replaces consumer-owned confirmed projections from the whole authoritative snapshot before Ready is published.</summary>
        void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot);
    }
}
