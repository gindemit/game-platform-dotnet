#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public enum StoredProjectionKind { Upsert, RemoveFromView, Tombstone }
    public sealed class StoredProjectionMutation
    {
        private readonly byte[] payload;
        public StoredProjectionMutation(string collection,string entityKey,long revision,StoredProjectionKind kind,byte[] payload){Collection=collection??throw new ArgumentNullException(nameof(collection));EntityKey=entityKey??throw new ArgumentNullException(nameof(entityKey));Revision=revision;Kind=kind;this.payload=(byte[])(payload??throw new ArgumentNullException(nameof(payload))).Clone();}
        public string Collection{get;}public string EntityKey{get;}public long Revision{get;}public StoredProjectionKind Kind{get;}public byte[] CopyPayload()=>(byte[])payload.Clone();
    }
    public sealed class BootstrapBoundary
    {
        private readonly byte[] session,firstPage;public BootstrapBoundary(ClientStreamId streamId,long committedThrough,long visibilityGeneration,Guid logEpoch,long expiresAt,IReadOnlyList<string> requiredCollections,byte[] session,byte[] firstPage){StreamId=streamId;CommittedThrough=committedThrough;VisibilityGeneration=visibilityGeneration;LogEpoch=logEpoch;ExpiresAt=expiresAt;RequiredCollections=requiredCollections;this.session=(byte[])session.Clone();this.firstPage=(byte[])firstPage.Clone();}
        public ClientStreamId StreamId{get;}public long CommittedThrough{get;}public long VisibilityGeneration{get;}public Guid LogEpoch{get;}public long ExpiresAt{get;}public IReadOnlyList<string> RequiredCollections{get;}public byte[] CopySession()=>(byte[])session.Clone();public byte[] CopyFirstPageToken()=>(byte[])firstPage.Clone();
    }
    public sealed class StagedBootstrapPage
    {
        private readonly byte[] session,requestedPage;private readonly byte[]? next,cursor;public StagedBootstrapPage(byte[] session,byte[] requestedPage,long committedThrough,IReadOnlyList<StoredProjectionMutation> entities,bool hasMore,byte[]? next,byte[]? cursor){this.session=(byte[])session.Clone();this.requestedPage=(byte[])requestedPage.Clone();CommittedThrough=committedThrough;Entities=entities;HasMore=hasMore;this.next=next==null?null:(byte[])next.Clone();this.cursor=cursor==null?null:(byte[])cursor.Clone();}
        public long CommittedThrough{get;}public IReadOnlyList<StoredProjectionMutation> Entities{get;}public bool HasMore{get;}public byte[] CopySession()=>(byte[])session.Clone();public byte[] CopyRequestedPageToken()=>(byte[])requestedPage.Clone();public byte[]? CopyNextPageToken()=>next==null?null:(byte[])next.Clone();public byte[]? CopyInitialPullCursor()=>cursor==null?null:(byte[])cursor.Clone();
    }
    public sealed class StoredPullGroup{public StoredPullGroup(long revision,IReadOnlyList<StoredProjectionMutation> changes){Revision=revision;Changes=changes;}public long Revision{get;}public IReadOnlyList<StoredProjectionMutation> Changes{get;}}
    public sealed class StoredPullPage{private readonly byte[] cursor;public StoredPullPage(long committedThrough,IReadOnlyList<StoredPullGroup> groups,byte[] cursor,bool hasMore){CommittedThrough=committedThrough;Groups=groups;this.cursor=(byte[])cursor.Clone();HasMore=hasMore;}public long CommittedThrough{get;}public IReadOnlyList<StoredPullGroup> Groups{get;}public bool HasMore{get;}public byte[] CopyNextCursor()=>(byte[])cursor.Clone();}
    public sealed class BootstrapProgress{private readonly byte[] session,pageToken;public BootstrapProgress(ClientStreamId streamId,long committedThrough,long visibilityGeneration,Guid logEpoch,long expiresAt,byte[] session,byte[] pageToken){StreamId=streamId;CommittedThrough=committedThrough;VisibilityGeneration=visibilityGeneration;LogEpoch=logEpoch;ExpiresAt=expiresAt;this.session=(byte[])session.Clone();this.pageToken=(byte[])pageToken.Clone();}public ClientStreamId StreamId{get;}public long CommittedThrough{get;}public long VisibilityGeneration{get;}public Guid LogEpoch{get;}public long ExpiresAt{get;}public byte[] CopySession()=>(byte[])session.Clone();public byte[] CopyPageToken()=>(byte[])pageToken.Clone();}
    public sealed class PullCheckpoint{private readonly byte[] cursor;public PullCheckpoint(ClientStreamId streamId,byte[] cursor,long committedThrough,long? fixedThrough){StreamId=streamId;this.cursor=(byte[])cursor.Clone();CommittedThrough=committedThrough;FixedThrough=fixedThrough;}public ClientStreamId StreamId{get;}public byte[] CopyCursor()=>(byte[])cursor.Clone();public long CommittedThrough{get;}public long? FixedThrough{get;}}
    public interface IPrivateSyncStore
    {
        Task<BootstrapProgress?> GetBootstrapProgressAsync(CancellationToken cancellationToken);
        Task BeginBootstrapAsync(BootstrapBoundary start,CancellationToken cancellationToken);
        Task StageBootstrapPageAsync(StagedBootstrapPage page,CancellationToken cancellationToken);
        Task<PullCheckpoint> GetPullCheckpointAsync(CancellationToken cancellationToken);
        Task ApplyPullPageAsync(StoredPullPage page,CancellationToken cancellationToken);
    }
}
