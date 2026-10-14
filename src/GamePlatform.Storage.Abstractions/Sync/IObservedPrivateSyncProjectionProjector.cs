using System.Collections.Generic;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public interface IObservedPrivateSyncProjectionProjector : IPrivateSyncProjectionProjector
    {
        void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group, PrivateSyncProjectionObservation observation); void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot, PrivateSyncProjectionObservation observation);
    }
}
