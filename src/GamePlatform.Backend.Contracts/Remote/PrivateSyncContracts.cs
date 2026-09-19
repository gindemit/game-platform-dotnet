#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{
    public sealed class SnapshotCollection
    {
        public SnapshotCollection(string name, int schemaVersion, bool required)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 64) throw new ArgumentOutOfRangeException(nameof(name));
            if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            Name = name; SchemaVersion = schemaVersion; Required = required;
        }
        public string Name { get; }
        public int SchemaVersion { get; }
        public bool Required { get; }
    }

    public enum ProjectionMutationKind { Upsert, RemoveFromView, Tombstone }

    public sealed class RemoteProjectionMutation
    {
        private readonly byte[] payload;
        public RemoteProjectionMutation(string collection, string entityKey, long entityRevision, ProjectionMutationKind kind, byte[]? payload)
        {
            if (string.IsNullOrWhiteSpace(collection) || collection.Length > 64) throw new ArgumentOutOfRangeException(nameof(collection));
            if (string.IsNullOrWhiteSpace(entityKey) || entityKey.Length > 512) throw new ArgumentOutOfRangeException(nameof(entityKey));
            if (entityRevision <= 0) throw new ArgumentOutOfRangeException(nameof(entityRevision));
            if (!Enum.IsDefined(typeof(ProjectionMutationKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == ProjectionMutationKind.Upsert && (payload == null || payload.Length == 0)) throw new ArgumentException("An upsert payload is required.", nameof(payload));
            if ((payload?.Length ?? 0) > 262_144) throw new ArgumentOutOfRangeException(nameof(payload));
            Collection = collection; EntityKey = entityKey; EntityRevision = entityRevision; Kind = kind; this.payload = payload == null ? Array.Empty<byte>() : (byte[])payload.Clone();
        }
        public string Collection { get; }
        public string EntityKey { get; }
        public long EntityRevision { get; }
        public ProjectionMutationKind Kind { get; }
        public byte[] CopyPayload() => (byte[])payload.Clone();
    }

    public sealed class BootstrapStart
    {
        private readonly byte[] session, firstPage;
        public BootstrapStart(ClientStreamId streamId, long finalizedThrough, long nextSequence, long committedThrough, long visibilityGeneration, Guid logEpoch, IReadOnlyList<SnapshotCollection> collections, byte[] session, byte[] firstPage, long expiresAt)
        {
            if (!streamId.IsValid || finalizedThrough < 0 || nextSequence <= 0 || committedThrough < 0 || visibilityGeneration < 0 || logEpoch == Guid.Empty) throw new ArgumentException("Invalid bootstrap boundary.");
            ValidateToken(session, nameof(session)); ValidateToken(firstPage, nameof(firstPage));
            if (collections == null) throw new ArgumentNullException(nameof(collections));
            if (expiresAt < 0 || expiresAt > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(expiresAt));
            StreamId = streamId; FinalizedThrough = finalizedThrough; NextSequence = nextSequence; CommittedThrough = committedThrough; VisibilityGeneration = visibilityGeneration; LogEpoch = logEpoch; Collections = Copy(collections); this.session = (byte[])session.Clone(); this.firstPage = (byte[])firstPage.Clone(); ExpiresAt = expiresAt;
        }
        public ClientStreamId StreamId { get; } public long FinalizedThrough { get; } public long NextSequence { get; } public long CommittedThrough { get; } public long VisibilityGeneration { get; } public Guid LogEpoch { get; } public IReadOnlyList<SnapshotCollection> Collections { get; } public long ExpiresAt { get; }
        public byte[] CopySession() => (byte[])session.Clone(); public byte[] CopyFirstPageToken() => (byte[])firstPage.Clone();
        private static IReadOnlyList<SnapshotCollection> Copy(IReadOnlyList<SnapshotCollection> values) { var copy = new SnapshotCollection[values.Count]; for (var i = 0; i < copy.Length; i++) copy[i] = values[i] ?? throw new ArgumentException("A collection is null.", nameof(values)); return Array.AsReadOnly(copy); }
        internal static void ValidateToken(byte[] value, string name) { if (value == null || value.Length < 12 || value.Length > 3072) throw new ArgumentOutOfRangeException(name); }
    }

    public sealed class BootstrapPage
    {
        private readonly byte[] session; private readonly byte[]? nextPage, initialCursor;
        public BootstrapPage(byte[] session, long committedThrough, IReadOnlyList<RemoteProjectionMutation> entities, bool hasMore, byte[]? nextPageToken, byte[]? initialPullCursor)
        {
            BootstrapStart.ValidateToken(session, nameof(session)); if (committedThrough < 0) throw new ArgumentOutOfRangeException(nameof(committedThrough)); if (entities == null || entities.Count > 1024) throw new ArgumentOutOfRangeException(nameof(entities));
            if (hasMore) BootstrapStart.ValidateToken(nextPageToken!, nameof(nextPageToken)); else BootstrapStart.ValidateToken(initialPullCursor!, nameof(initialPullCursor));
            if (hasMore && initialPullCursor != null || !hasMore && nextPageToken != null) throw new ArgumentException("Page continuation is inconsistent.");
            this.session=(byte[])session.Clone(); CommittedThrough=committedThrough; Entities=Copy(entities); HasMore=hasMore; nextPage=nextPageToken==null?null:(byte[])nextPageToken.Clone(); initialCursor=initialPullCursor==null?null:(byte[])initialPullCursor.Clone();
        }
        public long CommittedThrough { get; } public IReadOnlyList<RemoteProjectionMutation> Entities { get; } public bool HasMore { get; }
        public byte[] CopySession()=> (byte[])session.Clone(); public byte[]? CopyNextPageToken()=>nextPage==null?null:(byte[])nextPage.Clone(); public byte[]? CopyInitialPullCursor()=>initialCursor==null?null:(byte[])initialCursor.Clone();
        private static IReadOnlyList<RemoteProjectionMutation> Copy(IReadOnlyList<RemoteProjectionMutation> values) { var copy=new RemoteProjectionMutation[values.Count]; for(var i=0;i<copy.Length;i++) copy[i]=values[i]??throw new ArgumentException("An entity is null.",nameof(values)); return Array.AsReadOnly(copy); }
    }

    public sealed class RemotePullGroup
    {
        public RemotePullGroup(long feedRevision, IReadOnlyList<RemoteProjectionMutation> changes) { if(feedRevision<=0)throw new ArgumentOutOfRangeException(nameof(feedRevision)); if(changes==null||changes.Count==0)throw new ArgumentOutOfRangeException(nameof(changes)); FeedRevision=feedRevision; var copy=new RemoteProjectionMutation[changes.Count];for(var i=0;i<copy.Length;i++)copy[i]=changes[i]??throw new ArgumentException("A change is null.",nameof(changes));Changes=Array.AsReadOnly(copy); }
        public long FeedRevision{get;} public IReadOnlyList<RemoteProjectionMutation> Changes{get;}
    }
    public sealed class RemotePullPage
    {
        private readonly byte[]? nextCursor;
        private RemotePullPage(bool reset,string? reason,long committedThrough,IReadOnlyList<RemotePullGroup> groups,byte[]? cursor,bool hasMore){ResetRequired=reset;ResetReason=reason;CommittedThrough=committedThrough;Groups=groups;nextCursor=cursor==null?null:(byte[])cursor.Clone();HasMore=hasMore;}
        public static RemotePullPage Page(long committedThrough,IReadOnlyList<RemotePullGroup> groups,byte[] nextCursor,bool hasMore){if(committedThrough<0||groups==null)throw new ArgumentOutOfRangeException();BootstrapStart.ValidateToken(nextCursor,nameof(nextCursor));return new RemotePullPage(false,null,committedThrough,Copy(groups),nextCursor,hasMore);}
        public static RemotePullPage Reset(string reason){if(string.IsNullOrWhiteSpace(reason)||reason.Length>64)throw new ArgumentOutOfRangeException(nameof(reason));return new RemotePullPage(true,reason,0,Array.Empty<RemotePullGroup>(),null,false);}
        public bool ResetRequired{get;} public string? ResetReason{get;} public long CommittedThrough{get;} public IReadOnlyList<RemotePullGroup> Groups{get;} public bool HasMore{get;} public byte[]? CopyNextCursor()=>nextCursor==null?null:(byte[])nextCursor.Clone();
        private static IReadOnlyList<RemotePullGroup> Copy(IReadOnlyList<RemotePullGroup> values){var copy=new RemotePullGroup[values.Count];for(var i=0;i<copy.Length;i++)copy[i]=values[i]??throw new ArgumentException("A group is null.",nameof(values));return Array.AsReadOnly(copy);}
    }

    public interface IPrivateSyncRemote
    {
        Task<RemoteResult<BootstrapStart>> StartBootstrapAsync(ClientStreamId streamId, CancellationToken cancellationToken);
        Task<RemoteResult<BootstrapPage>> GetBootstrapPageAsync(byte[] session, byte[] pageToken, int maximumBytes, CancellationToken cancellationToken);
        Task<RemoteResult<RemotePullPage>> PullAsync(byte[] cursor, int maximumBytes, CancellationToken cancellationToken);
    }
}
