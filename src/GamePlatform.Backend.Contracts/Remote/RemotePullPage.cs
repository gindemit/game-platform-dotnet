using System;
using System.Collections.Generic;

namespace GamePlatform.Backend.Contracts.Remote
{
    public sealed class RemotePullPage
    {
        private readonly byte[]? nextCursor;
        private RemotePullPage(bool reset, string? reason, long committedThrough, IReadOnlyList<RemotePullGroup> groups, byte[]? cursor, bool hasMore, long serverTime)
        {
            if (serverTime < 0 || serverTime > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(serverTime));
            ResetRequired = reset;
            ResetReason = reason;
            CommittedThrough = committedThrough;
            Groups = groups;
            nextCursor = cursor == null ? null : (byte[])cursor.Clone();
            HasMore = hasMore;
            ServerTime = serverTime;
        }
        public static RemotePullPage Page(long committedThrough, IReadOnlyList<RemotePullGroup> groups, byte[] nextCursor, bool hasMore) => Page(committedThrough, groups, nextCursor, hasMore, 0);
        public static RemotePullPage Page(long committedThrough, IReadOnlyList<RemotePullGroup> groups, byte[] nextCursor, bool hasMore, long serverTime)
        {
            if (committedThrough < 0 || groups == null)
                throw new ArgumentOutOfRangeException();
            BootstrapStart.ValidateToken(nextCursor, nameof(nextCursor));
            return new RemotePullPage(false, null, committedThrough, Copy(groups), nextCursor, hasMore, serverTime);
        }
        public static RemotePullPage Reset(string reason) => Reset(reason, 0);
        public static RemotePullPage Reset(string reason, long serverTime)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 64)
                throw new ArgumentOutOfRangeException(nameof(reason));
            return new RemotePullPage(true, reason, 0, Array.Empty<RemotePullGroup>(), null, false, serverTime);
        }
        public bool ResetRequired { get; }
        public string? ResetReason { get; }
        public long CommittedThrough { get; }
        public IReadOnlyList<RemotePullGroup> Groups { get; }
        public bool HasMore { get; }
        public long ServerTime { get; }
        public byte[]? CopyNextCursor() => nextCursor == null ? null : (byte[])nextCursor.Clone();
        private static IReadOnlyList<RemotePullGroup> Copy(IReadOnlyList<RemotePullGroup> values)
        {
            var copy = new RemotePullGroup[values.Count];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = values[i] ?? throw new ArgumentException("A group is null.", nameof(values));
            return Array.AsReadOnly(copy);
        }
    }
}
