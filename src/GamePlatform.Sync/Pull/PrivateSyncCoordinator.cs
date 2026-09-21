#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Storage.Abstractions.Sync;

namespace GamePlatform.Sync.Pull
{
    public enum PrivateSyncResult { BootstrapInstalled, PageApplied, BoundaryComplete, ResetReconciled, RemoteFailure }
    public sealed class PrivateSyncCoordinator
    {
        private readonly IPrivateSyncStore store; private readonly IPrivateSyncRemote remote; private readonly Func<long> now;
        public PrivateSyncCoordinator(IPrivateSyncStore store, IPrivateSyncRemote remote, Func<long> nowMilliseconds) { this.store = store ?? throw new ArgumentNullException(nameof(store)); this.remote = remote ?? throw new ArgumentNullException(nameof(remote)); now = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds)); }
        public async Task<PrivateSyncResult> BootstrapAsync(GamePlatform.Core.ClientStreamId streamId, CancellationToken cancellationToken)
        {
            var progress = await store.GetBootstrapProgressAsync(cancellationToken).ConfigureAwait(false);
            if (progress == null || progress.StreamId != streamId || progress.ExpiresAt <= now()) { var started = await remote.StartBootstrapAsync(streamId, cancellationToken).ConfigureAwait(false); if (!started.IsSuccess) return PrivateSyncResult.RemoteFailure; await store.BeginBootstrapAsync(Boundary(started.Value!), cancellationToken).ConfigureAwait(false); progress = await store.GetBootstrapProgressAsync(cancellationToken).ConfigureAwait(false); }
            if (progress == null || progress.StreamId != streamId || progress.ExpiresAt <= now()) return PrivateSyncResult.RemoteFailure;
            while (true) { var requestedPage = progress.CopyPageToken(); var page = await remote.GetBootstrapPageAsync(progress.CopySession(), requestedPage, 262_144, cancellationToken).ConfigureAwait(false); if (!page.IsSuccess) return PrivateSyncResult.RemoteFailure; await store.StageBootstrapPageAsync(Staged(page.Value!, requestedPage), cancellationToken).ConfigureAwait(false); if (!page.Value!.HasMore) return PrivateSyncResult.BootstrapInstalled; progress = await store.GetBootstrapProgressAsync(cancellationToken).ConfigureAwait(false); if (progress == null) return PrivateSyncResult.RemoteFailure; }
        }
        public async Task<PrivateSyncResult> PullOnceAsync(CancellationToken cancellationToken)
        {
            var checkpoint = await store.GetPullCheckpointAsync(cancellationToken).ConfigureAwait(false); var pulled = await remote.PullAsync(checkpoint.CopyCursor(), 262_144, cancellationToken).ConfigureAwait(false); if (!pulled.IsSuccess) return PrivateSyncResult.RemoteFailure;
            if (pulled.Value!.ResetRequired) { var result = await BootstrapAsync(checkpoint.StreamId, cancellationToken).ConfigureAwait(false); return result == PrivateSyncResult.BootstrapInstalled ? PrivateSyncResult.ResetReconciled : result; }
            await store.ApplyPullPageAsync(Pulled(pulled.Value), cancellationToken).ConfigureAwait(false); return pulled.Value.HasMore ? PrivateSyncResult.PageApplied : PrivateSyncResult.BoundaryComplete;
        }
        private static BootstrapBoundary Boundary(BootstrapStart value) { var names = new string[value.Collections.Count]; for (var i = 0; i < names.Length; i++) { if (!value.Collections[i].Required || value.Collections[i].SchemaVersion != 1) throw new InvalidOperationException("The bootstrap manifest is unsupported."); names[i] = value.Collections[i].Name; } return new BootstrapBoundary(value.StreamId, value.StreamState, value.FinalizedThrough, value.NextSequence, value.CommittedThrough, value.VisibilityGeneration, value.LogEpoch, value.ExpiresAt, Array.AsReadOnly(names), value.CopySession(), value.CopyFirstPageToken()); }
        private static StoredProjectionMutation Mutation(RemoteProjectionMutation value) => new StoredProjectionMutation(value.Collection, value.EntityKey, value.EntityRevision, (StoredProjectionKind)(int)value.Kind, value.CopyPayload());
        private static StagedBootstrapPage Staged(BootstrapPage value, byte[] requestedPage) { var entities = new StoredProjectionMutation[value.Entities.Count]; for (var i = 0; i < entities.Length; i++) entities[i] = Mutation(value.Entities[i]); return new StagedBootstrapPage(value.CopySession(), requestedPage, value.CommittedThrough, Array.AsReadOnly(entities), value.HasMore, value.CopyNextPageToken(), value.CopyInitialPullCursor(), value.ServerTime); }
        private static StoredPullPage Pulled(RemotePullPage value) { var groups = new StoredPullGroup[value.Groups.Count]; for (var i = 0; i < groups.Length; i++) { var changes = new StoredProjectionMutation[value.Groups[i].Changes.Count]; for (var j = 0; j < changes.Length; j++) changes[j] = Mutation(value.Groups[i].Changes[j]); groups[i] = new StoredPullGroup(value.Groups[i].FeedRevision, Array.AsReadOnly(changes)); } return new StoredPullPage(value.CommittedThrough, Array.AsReadOnly(groups), value.CopyNextCursor()!, value.HasMore, value.ServerTime); }
    }
}
