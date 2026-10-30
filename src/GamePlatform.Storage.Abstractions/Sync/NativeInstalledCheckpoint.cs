using System;
namespace GamePlatform.Storage.Abstractions.Sync
{
    public sealed class NativeInstalledCheckpoint
    {
        public NativeInstalledCheckpoint(long committedThrough, long visibilityGeneration, Guid logEpoch)
        { CommittedThrough = committedThrough; VisibilityGeneration = visibilityGeneration; LogEpoch = logEpoch; }
        public long CommittedThrough { get; }
        public long VisibilityGeneration { get; }
        public Guid LogEpoch { get; }
    }
}
