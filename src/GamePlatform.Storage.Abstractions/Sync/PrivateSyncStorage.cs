#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;

namespace GamePlatform.Storage.Abstractions.Sync
{
    /// <summary>
    /// The durable raw-feed state. Invalidation is deliberately not a removal:
    /// it fences the named cached projection until its authorized read path
    /// refreshes it.
    /// </summary>
    public enum StoredProjectionKind { Upsert, RemoveFromView, Tombstone, Invalidation }
    public sealed class StoredProjectionMutation
    {
        private readonly byte[] payload;
        public StoredProjectionMutation(string collection, string entityKey, long revision, StoredProjectionKind kind, byte[] payload) { Collection = collection ?? throw new ArgumentNullException(nameof(collection)); EntityKey = entityKey ?? throw new ArgumentNullException(nameof(entityKey)); Revision = revision; Kind = kind; this.payload = (byte[])(payload ?? throw new ArgumentNullException(nameof(payload))).Clone(); }
        public string Collection { get; }
        public string EntityKey { get; }
        public long Revision { get; }
        public StoredProjectionKind Kind { get; }
        public byte[] CopyPayload() => (byte[])payload.Clone();
    }
    public sealed class BootstrapBoundary
    {
        private readonly byte[] session, firstPage; public BootstrapBoundary(ClientStreamId streamId, string streamState, long finalizedThrough, long? nextSequence, long committedThrough, long visibilityGeneration, Guid logEpoch, long expiresAt, IReadOnlyList<string> requiredCollections, byte[] session, byte[] firstPage) { StreamId = streamId; StreamState = streamState ?? throw new ArgumentNullException(nameof(streamState)); FinalizedThrough = finalizedThrough; NextSequence = nextSequence; CommittedThrough = committedThrough; VisibilityGeneration = visibilityGeneration; LogEpoch = logEpoch; ExpiresAt = expiresAt; RequiredCollections = Copy(requiredCollections); this.session = (byte[])session.Clone(); this.firstPage = (byte[])firstPage.Clone(); }
        public ClientStreamId StreamId { get; }
        public string StreamState { get; }
        public long FinalizedThrough { get; }
        public long? NextSequence { get; }
        public long CommittedThrough { get; }
        public long VisibilityGeneration { get; }
        public Guid LogEpoch { get; }
        public long ExpiresAt { get; }
        public IReadOnlyList<string> RequiredCollections { get; }
        public byte[] CopySession() => (byte[])session.Clone(); public byte[] CopyFirstPageToken() => (byte[])firstPage.Clone();
        private static IReadOnlyList<string> Copy(IReadOnlyList<string> values) { if (values == null) throw new ArgumentNullException(nameof(values)); var copy = new string[values.Count]; for (var i = 0; i < copy.Length; i++) copy[i] = values[i] ?? throw new ArgumentException("A collection is null.", nameof(values)); return Array.AsReadOnly(copy); }
    }
    public sealed class StagedBootstrapPage
    {
        private readonly byte[] session, requestedPage; private readonly byte[]? next, cursor; public StagedBootstrapPage(byte[] session, byte[] requestedPage, long committedThrough, IReadOnlyList<StoredProjectionMutation> entities, bool hasMore, byte[]? next, byte[]? cursor) : this(session, requestedPage, committedThrough, entities, hasMore, next, cursor, 0) { }
        public StagedBootstrapPage(byte[] session, byte[] requestedPage, long committedThrough, IReadOnlyList<StoredProjectionMutation> entities, bool hasMore, byte[]? next, byte[]? cursor, long serverTime) { if (serverTime < 0 || serverTime > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(serverTime)); this.session = (byte[])session.Clone(); this.requestedPage = (byte[])requestedPage.Clone(); CommittedThrough = committedThrough; Entities = Copy(entities); HasMore = hasMore; this.next = next == null ? null : (byte[])next.Clone(); this.cursor = cursor == null ? null : (byte[])cursor.Clone(); ServerTime = serverTime; }
        public long CommittedThrough { get; }
        public IReadOnlyList<StoredProjectionMutation> Entities { get; }
        public bool HasMore { get; }
        public long ServerTime { get; }
        public byte[] CopySession() => (byte[])session.Clone(); public byte[] CopyRequestedPageToken() => (byte[])requestedPage.Clone(); public byte[]? CopyNextPageToken() => next == null ? null : (byte[])next.Clone(); public byte[]? CopyInitialPullCursor() => cursor == null ? null : (byte[])cursor.Clone();
        private static IReadOnlyList<StoredProjectionMutation> Copy(IReadOnlyList<StoredProjectionMutation> values) { if (values == null) throw new ArgumentNullException(nameof(values)); var copy = new StoredProjectionMutation[values.Count]; for (var i = 0; i < copy.Length; i++) copy[i] = values[i] ?? throw new ArgumentException("An entity is null.", nameof(values)); return Array.AsReadOnly(copy); }
    }
    public sealed class StoredPullGroup { public StoredPullGroup(long revision, IReadOnlyList<StoredProjectionMutation> changes) { Revision = revision; Changes = Copy(changes); } public long Revision { get; } public IReadOnlyList<StoredProjectionMutation> Changes { get; } private static IReadOnlyList<StoredProjectionMutation> Copy(IReadOnlyList<StoredProjectionMutation> values) { if (values == null) throw new ArgumentNullException(nameof(values)); var copy = new StoredProjectionMutation[values.Count]; for (var i = 0; i < copy.Length; i++) copy[i] = values[i] ?? throw new ArgumentException("A change is null.", nameof(values)); return Array.AsReadOnly(copy); } }
    /// <summary>Projects confirmed private-feed data in the SQLite transaction that changed its raw rows.</summary>
    public interface IPrivateSyncProjectionProjector
    {
        /// <summary>The group is supplied in exact feed order and its revision remains its feed correlation.</summary>
        void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group);
        /// <summary>Replaces consumer-owned confirmed projections from the whole authoritative snapshot before Ready is published.</summary>
        void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot);
    }
    public readonly struct PrivateSyncProjectionObservation { public PrivateSyncProjectionObservation(long fixedThrough, long serverTimeMilliseconds) { if (fixedThrough < 0) throw new ArgumentOutOfRangeException(nameof(fixedThrough)); if (serverTimeMilliseconds < 0 || serverTimeMilliseconds > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(serverTimeMilliseconds)); FixedThrough = fixedThrough; ServerTimeMilliseconds = serverTimeMilliseconds; } public long FixedThrough { get; } public long ServerTimeMilliseconds { get; } }
    public interface IObservedPrivateSyncProjectionProjector : IPrivateSyncProjectionProjector { void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group, PrivateSyncProjectionObservation observation); void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot, PrivateSyncProjectionObservation observation); }
    public sealed class StoredPullPage { private readonly byte[] cursor; public StoredPullPage(long committedThrough, IReadOnlyList<StoredPullGroup> groups, byte[] cursor, bool hasMore) : this(committedThrough, groups, cursor, hasMore, 0) { } public StoredPullPage(long committedThrough, IReadOnlyList<StoredPullGroup> groups, byte[] cursor, bool hasMore, long serverTime) { if (serverTime < 0 || serverTime > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(serverTime)); CommittedThrough = committedThrough; Groups = Copy(groups); this.cursor = (byte[])cursor.Clone(); HasMore = hasMore; ServerTime = serverTime; } public long CommittedThrough { get; } public IReadOnlyList<StoredPullGroup> Groups { get; } public bool HasMore { get; } public long ServerTime { get; } public byte[] CopyNextCursor() => (byte[])cursor.Clone(); private static IReadOnlyList<StoredPullGroup> Copy(IReadOnlyList<StoredPullGroup> values) { if (values == null) throw new ArgumentNullException(nameof(values)); var copy = new StoredPullGroup[values.Count]; for (var i = 0; i < copy.Length; i++) copy[i] = values[i] ?? throw new ArgumentException("A group is null.", nameof(values)); return Array.AsReadOnly(copy); } }
    public sealed class BootstrapProgress { private readonly byte[] session, pageToken; public BootstrapProgress(ClientStreamId streamId, long committedThrough, long visibilityGeneration, Guid logEpoch, long expiresAt, byte[] session, byte[] pageToken) { StreamId = streamId; CommittedThrough = committedThrough; VisibilityGeneration = visibilityGeneration; LogEpoch = logEpoch; ExpiresAt = expiresAt; this.session = (byte[])session.Clone(); this.pageToken = (byte[])pageToken.Clone(); } public ClientStreamId StreamId { get; } public long CommittedThrough { get; } public long VisibilityGeneration { get; } public Guid LogEpoch { get; } public long ExpiresAt { get; } public byte[] CopySession() => (byte[])session.Clone(); public byte[] CopyPageToken() => (byte[])pageToken.Clone(); }
    public sealed class PullCheckpoint { private readonly byte[] cursor; public PullCheckpoint(ClientStreamId streamId, byte[] cursor, long committedThrough, long? fixedThrough) { StreamId = streamId; this.cursor = (byte[])cursor.Clone(); CommittedThrough = committedThrough; FixedThrough = fixedThrough; } public ClientStreamId StreamId { get; } public byte[] CopyCursor() => (byte[])cursor.Clone(); public long CommittedThrough { get; } public long? FixedThrough { get; } }
    public interface IPrivateSyncStore
    {
        Task<BootstrapProgress?> GetBootstrapProgressAsync(CancellationToken cancellationToken);
        Task BeginBootstrapAsync(BootstrapBoundary start, CancellationToken cancellationToken);
        Task StageBootstrapPageAsync(StagedBootstrapPage page, CancellationToken cancellationToken);
        Task<PullCheckpoint> GetPullCheckpointAsync(CancellationToken cancellationToken);
        Task ApplyPullPageAsync(StoredPullPage page, CancellationToken cancellationToken);
    }
}
