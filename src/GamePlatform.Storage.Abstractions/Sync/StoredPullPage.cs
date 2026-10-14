using System;
using System.Collections.Generic;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public sealed class StoredPullPage
    {
        private readonly byte[] cursor; public StoredPullPage(long committedThrough, IReadOnlyList<StoredPullGroup> groups, byte[] cursor, bool hasMore) : this(committedThrough, groups, cursor, hasMore, 0) { }
        public StoredPullPage(long committedThrough, IReadOnlyList<StoredPullGroup> groups, byte[] cursor, bool hasMore, long serverTime)
        {
            if (serverTime < 0 || serverTime > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(serverTime));
            CommittedThrough = committedThrough;
            Groups = Copy(groups);
            this.cursor = (byte[])cursor.Clone();
            HasMore = hasMore;
            ServerTime = serverTime;
        }
        public long CommittedThrough { get; }
        public IReadOnlyList<StoredPullGroup> Groups { get; }
        public bool HasMore { get; }
        public long ServerTime { get; }
        public byte[] CopyNextCursor() => (byte[])cursor.Clone(); private static IReadOnlyList<StoredPullGroup> Copy(IReadOnlyList<StoredPullGroup> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            var copy = new StoredPullGroup[values.Count];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = values[i] ?? throw new ArgumentException("A group is null.", nameof(values));
            return Array.AsReadOnly(copy);
        }
    }
}
