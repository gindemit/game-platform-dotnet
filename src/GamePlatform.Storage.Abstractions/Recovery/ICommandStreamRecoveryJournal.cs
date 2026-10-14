using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Recovery
{

    /// <summary>Durable crash-recovery journal sharing the persistent principal directory lifetime.</summary>
    public interface ICommandStreamRecoveryJournal
    {
        Task<CommandStreamRecoveryRecord?> LoadAsync(CancellationToken cancellationToken);
        Task<CommandStreamRecoveryRecord> PrepareAsync(CommandStreamRecoveryRequest request, CancellationToken cancellationToken);
        Task<CommandStreamRecoveryRecord> RecordRemoteCommitAsync(Guid recoveryId, Guid returnedRecoveryId, ClientStreamId priorStreamId,
            ClientStreamId successorStreamId, long priorFinalizedThrough, long nextSequence, CancellationToken cancellationToken);
        Task<CommandStreamRecoveryRecord> MarkExpiredAsync(Guid recoveryId, CancellationToken cancellationToken);
        Task<CommandStreamRecoveryRecord> AdoptAsync(Guid recoveryId, CancellationToken cancellationToken);
        Task<CommandStreamRecoveryRecord> MarkBootstrapCompleteAsync(Guid recoveryId, CancellationToken cancellationToken);
    }
}
