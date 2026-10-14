using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public interface IObservedPrivateSyncProjectionProjector : IPrivateSyncProjectionProjector
    {
        void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group, PrivateSyncProjectionObservation observation); void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot, PrivateSyncProjectionObservation observation);
    }
}
