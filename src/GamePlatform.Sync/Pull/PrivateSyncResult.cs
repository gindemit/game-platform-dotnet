using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Abstractions.Sync;

namespace GamePlatform.Sync.Pull
{
    public enum PrivateSyncResult
    {
        BootstrapInstalled,
        PageApplied,
        BoundaryComplete,
        ResetReconciled,
        RemoteFailure,
        StreamRecovering
    }
}
