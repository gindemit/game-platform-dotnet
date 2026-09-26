#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Abstractions.Sync;

namespace GamePlatform.Sync.Pull
{
    public enum PrivateSyncResult { BootstrapInstalled, PageApplied, BoundaryComplete, ResetReconciled, RemoteFailure, StreamRecovering }
    public sealed class PrivateSyncCoordinator
    {
        private enum BoundaryReconciliation { Current, Stale, Demoted, RemoteFailure }
        private readonly IPrivateSyncStore store; private readonly IPrivateSyncRemote remote; private readonly ICommandReceiptRemote? receipts; private readonly Func<long> now;
        public PrivateSyncCoordinator(IPrivateSyncStore store, IPrivateSyncRemote remote, Func<long> nowMilliseconds) { this.store = store ?? throw new ArgumentNullException(nameof(store)); this.remote = remote ?? throw new ArgumentNullException(nameof(remote)); now = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds)); }
        /// <summary>
        /// Also recovers a server stream whose finalized sequence regressed below local terminal state.
        /// </summary>
        public PrivateSyncCoordinator(IPrivateSyncStore store, IPrivateSyncRemote remote, ICommandReceiptRemote receipts, Func<long> nowMilliseconds) : this(store, remote, nowMilliseconds) { this.receipts = receipts ?? throw new ArgumentNullException(nameof(receipts)); }
        public async Task<PrivateSyncResult> BootstrapAsync(GamePlatform.Core.ClientStreamId streamId, CancellationToken cancellationToken)
        {
            var progress = await store.GetBootstrapProgressAsync(cancellationToken).ConfigureAwait(false);
            for (var restarted = false; ; restarted = true)
            {
                if (restarted || progress == null || progress.StreamId != streamId || progress.ExpiresAt <= now()) { var started = await remote.StartBootstrapAsync(streamId, cancellationToken).ConfigureAwait(false); if (!started.IsSuccess) return PrivateSyncResult.RemoteFailure; await store.BeginBootstrapAsync(Boundary(started.Value!), cancellationToken).ConfigureAwait(false); progress = await store.GetBootstrapProgressAsync(cancellationToken).ConfigureAwait(false); }
                if (progress == null || progress.StreamId != streamId || progress.ExpiresAt <= now()) return PrivateSyncResult.RemoteFailure;
                if (receipts == null) break;
                var reconciliation = await ReconcileRegressedBoundaryAsync(receipts, cancellationToken).ConfigureAwait(false);
                if (reconciliation == BoundaryReconciliation.Current) break;
                if (reconciliation == BoundaryReconciliation.Stale && !restarted) continue;
                if (reconciliation == BoundaryReconciliation.Stale) throw new StorageException(StorageFailure.StreamRecoveryRequired, "The bootstrap boundary stayed behind a retained receipt.");
                return reconciliation == BoundaryReconciliation.Demoted ? PrivateSyncResult.StreamRecovering : PrivateSyncResult.RemoteFailure;
            }
            while (true) { var requestedPage = progress.CopyPageToken(); var page = await remote.GetBootstrapPageAsync(progress.CopySession(), requestedPage, 262_144, cancellationToken).ConfigureAwait(false); if (!page.IsSuccess) return PrivateSyncResult.RemoteFailure; await store.StageBootstrapPageAsync(Staged(page.Value!, requestedPage), cancellationToken).ConfigureAwait(false); if (!page.Value!.HasMore) return PrivateSyncResult.BootstrapInstalled; progress = await store.GetBootstrapProgressAsync(cancellationToken).ConfigureAwait(false); if (progress == null) return PrivateSyncResult.RemoteFailure; }
        }
        public async Task<PrivateSyncResult> PullOnceAsync(CancellationToken cancellationToken)
        {
            var checkpoint = await store.GetPullCheckpointAsync(cancellationToken).ConfigureAwait(false); var pulled = await remote.PullAsync(checkpoint.CopyCursor(), 262_144, cancellationToken).ConfigureAwait(false); if (!pulled.IsSuccess) return PrivateSyncResult.RemoteFailure;
            if (pulled.Value!.ResetRequired) { var result = await BootstrapAsync(checkpoint.StreamId, cancellationToken).ConfigureAwait(false); return result == PrivateSyncResult.BootstrapInstalled ? PrivateSyncResult.ResetReconciled : result; }
            await store.ApplyPullPageAsync(Pulled(pulled.Value), cancellationToken).ConfigureAwait(false); return pulled.Value.HasMore ? PrivateSyncResult.PageApplied : PrivateSyncResult.BoundaryComplete;
        }
        /// <remarks>
        /// An absent receipt licenses replay only while the server reports the staged boundary as finalized.
        /// </remarks>
        private async Task<BoundaryReconciliation> ReconcileRegressedBoundaryAsync(ICommandReceiptRemote lookup, CancellationToken cancellationToken)
        {
            var terminal = await store.ReadTerminalCommandsAboveBootstrapBoundaryAsync(cancellationToken).ConfigureAwait(false);
            if (terminal.Count == 0) return BoundaryReconciliation.Current;
            var boundary = terminal[0].Command.Sequence - 1;
            // ponytail: one sequential lookup per lost command, repeated after any failure; persist per-command evidence if large gaps appear.
            foreach (var local in terminal)
            {
                var c = local.Command;
                var receipt = await lookup.LookupAsync(new RemoteCommand(c.OperationId, c.StreamId, c.InstallationId, c.Sequence, c.OperationKind, c.SchemaVersion, c.FingerprintVersion, c.ClientCreatedAt, c.CopyBody(), c.CopyFingerprint()), cancellationToken).ConfigureAwait(false);
                if (!receipt.IsSuccess && receipt.Failure.Kind == RemoteFailureKind.Conflict)
                    throw new StorageException(StorageFailure.StreamRecoveryRequired, "The receipt lookup conflicts with a local command identity.");
                if (!receipt.IsSuccess) return BoundaryReconciliation.RemoteFailure;
                if (!receipt.Value!.Found)
                {
                    if (receipt.Value.ObservedFinalizedThrough != boundary) return BoundaryReconciliation.Stale;
                    continue;
                }
                if ((receipt.Value.Outcome!.Status == RemoteCommandStatus.Accepted) != (local.Outcome == CommandTerminalOutcome.Accepted))
                    throw new StorageException(StorageFailure.StreamRecoveryRequired, "A retained receipt conflicts with the local terminal result.");
                return BoundaryReconciliation.Stale;
            }
            await store.DemoteTerminalCommandsAboveBootstrapBoundaryAsync(terminal[terminal.Count - 1].Command.Sequence, cancellationToken).ConfigureAwait(false);
            return BoundaryReconciliation.Demoted;
        }
        private static BootstrapBoundary Boundary(BootstrapStart value) { var names = new string[value.Collections.Count]; for (var i = 0; i < names.Length; i++) { if (!value.Collections[i].Required || value.Collections[i].SchemaVersion != 1) throw new InvalidOperationException("The bootstrap manifest is unsupported."); names[i] = value.Collections[i].Name; } return new BootstrapBoundary(value.StreamId, value.StreamState, value.FinalizedThrough, value.NextSequence, value.CommittedThrough, value.VisibilityGeneration, value.LogEpoch, value.ExpiresAt, Array.AsReadOnly(names), value.CopySession(), value.CopyFirstPageToken()); }
        private static StoredProjectionMutation Mutation(RemoteProjectionMutation value) => new StoredProjectionMutation(value.Collection, value.EntityKey, value.EntityRevision, (StoredProjectionKind)(int)value.Kind, value.CopyPayload());
        private static StagedBootstrapPage Staged(BootstrapPage value, byte[] requestedPage) { var entities = new StoredProjectionMutation[value.Entities.Count]; for (var i = 0; i < entities.Length; i++) entities[i] = Mutation(value.Entities[i]); return new StagedBootstrapPage(value.CopySession(), requestedPage, value.CommittedThrough, Array.AsReadOnly(entities), value.HasMore, value.CopyNextPageToken(), value.CopyInitialPullCursor(), value.ServerTime); }
        private static StoredPullPage Pulled(RemotePullPage value) { var groups = new StoredPullGroup[value.Groups.Count]; for (var i = 0; i < groups.Length; i++) { var changes = new StoredProjectionMutation[value.Groups[i].Changes.Count]; for (var j = 0; j < changes.Length; j++) changes[j] = Mutation(value.Groups[i].Changes[j]); groups[i] = new StoredPullGroup(value.Groups[i].FeedRevision, Array.AsReadOnly(changes)); } return new StoredPullPage(value.CommittedThrough, Array.AsReadOnly(groups), value.CopyNextCursor()!, value.HasMore, value.ServerTime); }
    }
}
