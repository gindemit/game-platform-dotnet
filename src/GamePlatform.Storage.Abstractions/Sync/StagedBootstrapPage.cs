using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public sealed class StagedBootstrapPage
    {
        private readonly byte[] session, requestedPage; private readonly byte[]? next, cursor; public StagedBootstrapPage(byte[] session, byte[] requestedPage, long committedThrough, IReadOnlyList<StoredProjectionMutation> entities, bool hasMore, byte[]? next, byte[]? cursor) : this(session, requestedPage, committedThrough, entities, hasMore, next, cursor, 0) { }
        public StagedBootstrapPage(byte[] session, byte[] requestedPage, long committedThrough, IReadOnlyList<StoredProjectionMutation> entities, bool hasMore, byte[]? next, byte[]? cursor, long serverTime)
        {
            if (serverTime < 0 || serverTime > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(serverTime));
            this.session = (byte[])session.Clone();
            this.requestedPage = (byte[])requestedPage.Clone();
            CommittedThrough = committedThrough;
            Entities = Copy(entities);
            HasMore = hasMore;
            this.next = next == null ? null : (byte[])next.Clone();
            this.cursor = cursor == null ? null : (byte[])cursor.Clone();
            ServerTime = serverTime;
        }
        public long CommittedThrough { get; }
        public IReadOnlyList<StoredProjectionMutation> Entities { get; }
        public bool HasMore { get; }
        public long ServerTime { get; }
        public byte[] CopySession() => (byte[])session.Clone(); public byte[] CopyRequestedPageToken() => (byte[])requestedPage.Clone(); public byte[]? CopyNextPageToken() => next == null ? null : (byte[])next.Clone(); public byte[]? CopyInitialPullCursor() => cursor == null ? null : (byte[])cursor.Clone();
        private static IReadOnlyList<StoredProjectionMutation> Copy(IReadOnlyList<StoredProjectionMutation> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            var copy = new StoredProjectionMutation[values.Count];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = values[i] ?? throw new ArgumentException("An entity is null.", nameof(values));
            return Array.AsReadOnly(copy);
        }
    }
}
