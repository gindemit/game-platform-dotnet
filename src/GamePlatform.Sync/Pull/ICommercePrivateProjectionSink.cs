using System.Collections.Generic;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Sync;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Sync.Pull
{
    /// <summary>Installs the frozen stack inventory and entitlement representation in the borrowed cursor transaction.</summary>
    public interface ICommercePrivateProjectionSink : IG3PrivateProjectionSink
    {
        void ReplaceCommerceSnapshot(ILocalStorageTransaction transaction, PrivateSyncProjectionObservation observation,
            ProjectionProfileSnapshot? profile, IReadOnlyList<ProjectionProgressionSnapshot> progression,
            IReadOnlyList<ProjectionWalletSnapshot> wallet, IReadOnlyList<ProjectionInventorySnapshot> inventory,
            IReadOnlyList<ProjectionEntitlementSnapshot> entitlements);
    }
}
