using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public sealed class PullCheckpoint
    {
        private readonly byte[] cursor; public PullCheckpoint(ClientStreamId streamId, byte[] cursor, long committedThrough, long? fixedThrough)
        {
            StreamId = streamId;
            this.cursor = (byte[])cursor.Clone();
            CommittedThrough = committedThrough;
            FixedThrough = fixedThrough;
        }
        public ClientStreamId StreamId { get; }
        public byte[] CopyCursor() => (byte[])cursor.Clone(); public long CommittedThrough { get; }
        public long? FixedThrough { get; }
    }
}
