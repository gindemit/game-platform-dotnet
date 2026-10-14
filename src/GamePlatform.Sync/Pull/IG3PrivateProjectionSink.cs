using System;
using System.Collections.Generic;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Sync;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Sync.Pull
{
    /// <summary>Consumer-owned typed installation boundary for the deliberately narrow G3 projection set.</summary>
    public interface IG3PrivateProjectionSink
    {
        void ReplaceSnapshot(ILocalStorageTransaction transaction, PrivateSyncProjectionObservation observation,
            ProjectionProfileSnapshot? profile, IReadOnlyList<ProjectionProgressionSnapshot> progression,
            IReadOnlyList<ProjectionWalletSnapshot> wallet);
        void ApplyGroup(ILocalStorageTransaction transaction, long feedRevision, PrivateSyncProjectionObservation observation,
            IReadOnlyList<IProjectionChange> changes);
    }
}
