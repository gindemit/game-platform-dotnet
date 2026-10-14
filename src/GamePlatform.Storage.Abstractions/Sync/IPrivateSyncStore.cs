using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public interface IPrivateSyncStore
    {
        Task<BootstrapProgress?> GetBootstrapProgressAsync(CancellationToken cancellationToken);
        Task BeginBootstrapAsync(BootstrapBoundary start, CancellationToken cancellationToken);
        Task StageBootstrapPageAsync(StagedBootstrapPage page, CancellationToken cancellationToken);
        Task<PullCheckpoint> GetPullCheckpointAsync(CancellationToken cancellationToken);
        Task ApplyPullPageAsync(StoredPullPage page, CancellationToken cancellationToken);
        /// <summary>
        /// Lists, in sequence order, the local terminal commands above the staged active boundary's finalized sequence.
        /// </summary>
        Task<IReadOnlyList<StoredTerminalCommand>> ReadTerminalCommandsAboveBootstrapBoundaryAsync(CancellationToken cancellationToken);
        /// <summary>
        /// Returns those commands to pending with their original identity, lowers the local finalized sequence to the boundary and discards the staged bootstrap.
        /// </summary>
        Task DemoteTerminalCommandsAboveBootstrapBoundaryAsync(long localFinalizedThrough, CancellationToken cancellationToken);
    }
}
