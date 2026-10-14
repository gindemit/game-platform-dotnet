using System;
using System.Collections.Generic;

namespace GamePlatform.Backend.Contracts.Remote
{

    public sealed class BootstrapPage
    {
        private readonly byte[] session; private readonly byte[]? nextPage, initialCursor;
        public BootstrapPage(byte[] session, long committedThrough, IReadOnlyList<RemoteProjectionMutation> entities, bool hasMore, byte[]? nextPageToken, byte[]? initialPullCursor)
            : this(session, committedThrough, entities, hasMore, nextPageToken, initialPullCursor, 0) { }
        public BootstrapPage(byte[] session, long committedThrough, IReadOnlyList<RemoteProjectionMutation> entities, bool hasMore, byte[]? nextPageToken, byte[]? initialPullCursor, long serverTime)
        {
            BootstrapStart.ValidateToken(session, nameof(session));
            if (committedThrough < 0)
                throw new ArgumentOutOfRangeException(nameof(committedThrough));
            if (entities == null || entities.Count > 1024)
                throw new ArgumentOutOfRangeException(nameof(entities));
            if (hasMore)
                BootstrapStart.ValidateToken(nextPageToken!, nameof(nextPageToken));
            else
                BootstrapStart.ValidateToken(initialPullCursor!, nameof(initialPullCursor));
            if (hasMore && initialPullCursor != null || !hasMore && nextPageToken != null)
                throw new ArgumentException("Page continuation is inconsistent.");
            if (serverTime < 0 || serverTime > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(serverTime));
            this.session = (byte[])session.Clone();
            CommittedThrough = committedThrough;
            Entities = Copy(entities);
            HasMore = hasMore;
            nextPage = nextPageToken == null ? null : (byte[])nextPageToken.Clone();
            initialCursor = initialPullCursor == null ? null : (byte[])initialPullCursor.Clone();
            ServerTime = serverTime;
        }
        public long CommittedThrough { get; }
        public IReadOnlyList<RemoteProjectionMutation> Entities { get; }
        public bool HasMore { get; }
        public long ServerTime { get; }
        public byte[] CopySession() => (byte[])session.Clone(); public byte[]? CopyNextPageToken() => nextPage == null ? null : (byte[])nextPage.Clone(); public byte[]? CopyInitialPullCursor() => initialCursor == null ? null : (byte[])initialCursor.Clone();
        private static IReadOnlyList<RemoteProjectionMutation> Copy(IReadOnlyList<RemoteProjectionMutation> values)
        {
            var copy = new RemoteProjectionMutation[values.Count];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = values[i] ?? throw new ArgumentException("An entity is null.", nameof(values));
            return Array.AsReadOnly(copy);
        }
    }
}
