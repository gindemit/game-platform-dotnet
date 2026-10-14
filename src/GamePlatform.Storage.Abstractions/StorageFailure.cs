using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions
{

    public enum StorageFailure
    {
        Busy,
        Capacity,
        Io,
        Corrupt,
        Constraint,
        Migration,
        InvalidOwner,
        Closing,
        NotReady,
        IdentityConflict,
        SequenceExhausted,
        StreamRecoveryRequired,
        Unavailable
    }
}
