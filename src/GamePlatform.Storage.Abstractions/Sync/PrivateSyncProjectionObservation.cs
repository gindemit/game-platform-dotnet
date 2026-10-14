using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public readonly struct PrivateSyncProjectionObservation
    {
        public PrivateSyncProjectionObservation(long fixedThrough, long serverTimeMilliseconds)
        {
            if (fixedThrough < 0)
                throw new ArgumentOutOfRangeException(nameof(fixedThrough));
            if (serverTimeMilliseconds < 0 || serverTimeMilliseconds > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(serverTimeMilliseconds));
            FixedThrough = fixedThrough;
            ServerTimeMilliseconds = serverTimeMilliseconds;
        }
        public long FixedThrough { get; }
        public long ServerTimeMilliseconds { get; }
    }
}
