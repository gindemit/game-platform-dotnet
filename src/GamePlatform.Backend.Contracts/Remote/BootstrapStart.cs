using System;
using System.Collections.Generic;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public sealed class BootstrapStart
    {
        private readonly byte[] session, firstPage;
        public BootstrapStart(ClientStreamId streamId, string streamState, long finalizedThrough, long? nextSequence, long committedThrough, long visibilityGeneration, Guid logEpoch, IReadOnlyList<SnapshotCollection> collections, byte[] session, byte[] firstPage, long expiresAt)
            : this(streamId, streamState, finalizedThrough, nextSequence, committedThrough, visibilityGeneration, logEpoch, collections, session, firstPage, expiresAt, 0) { }
        public BootstrapStart(ClientStreamId streamId, string streamState, long finalizedThrough, long? nextSequence, long committedThrough, long visibilityGeneration, Guid logEpoch, IReadOnlyList<SnapshotCollection> collections, byte[] session, byte[] firstPage, long expiresAt, long serverTime)
        {
            if (!streamId.IsValid || (streamState != "active" && streamState != "retired") || finalizedThrough < 0 || committedThrough < 0 || visibilityGeneration < 0 || logEpoch == Guid.Empty)
                throw new ArgumentException("Invalid bootstrap boundary.");
            if (streamState == "retired" ? nextSequence != null : finalizedThrough == long.MaxValue || nextSequence == null || nextSequence <= 0 || nextSequence != finalizedThrough + 1)
                throw new ArgumentException("Invalid authoritative stream sequence boundary.");
            ValidateToken(session, nameof(session));
            ValidateToken(firstPage, nameof(firstPage));
            if (collections == null)
                throw new ArgumentNullException(nameof(collections));
            if (expiresAt < 0 || expiresAt > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(expiresAt));
            if (serverTime < 0 || serverTime > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(serverTime));
            StreamId = streamId;
            StreamState = streamState;
            FinalizedThrough = finalizedThrough;
            NextSequence = nextSequence;
            CommittedThrough = committedThrough;
            VisibilityGeneration = visibilityGeneration;
            LogEpoch = logEpoch;
            Collections = Copy(collections);
            this.session = (byte[])session.Clone();
            this.firstPage = (byte[])firstPage.Clone();
            ExpiresAt = expiresAt;
            ServerTime = serverTime;
        }
        public ClientStreamId StreamId { get; }
        public string StreamState { get; }
        public long FinalizedThrough { get; }
        public long? NextSequence { get; }
        public long CommittedThrough { get; }
        public long VisibilityGeneration { get; }
        public Guid LogEpoch { get; }
        public IReadOnlyList<SnapshotCollection> Collections { get; }
        public long ExpiresAt { get; }
        public long ServerTime { get; }
        public byte[] CopySession() => (byte[])session.Clone(); public byte[] CopyFirstPageToken() => (byte[])firstPage.Clone();
        private static IReadOnlyList<SnapshotCollection> Copy(IReadOnlyList<SnapshotCollection> values)
        {
            var copy = new SnapshotCollection[values.Count];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = values[i] ?? throw new ArgumentException("A collection is null.", nameof(values));
            return Array.AsReadOnly(copy);
        }
        internal static void ValidateToken(byte[] value, string name)
        {
            if (value == null || value.Length < 12 || value.Length > 3072)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
