using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Storage.Abstractions.Sync
{
    /// <summary>
    /// The durable raw-feed state. Invalidation is deliberately not a removal:
    /// it fences the named cached projection until its authorized read path
    /// refreshes it.
    /// </summary>
    public enum StoredProjectionKind
    {
        Upsert, RemoveFromView, Tombstone, Invalidation
    }
}
