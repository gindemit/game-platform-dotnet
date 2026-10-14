using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Snapshots
{
    public enum SnapshotFreshness
    {
        Missing = 0,
        Stale = 1,
        Current = 2
    }
}
