using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Abstractions.Recovery;
using JournalRequest = GamePlatform.Storage.Abstractions.Recovery.CommandStreamRecoveryRequest;
using RemoteRequest = GamePlatform.Backend.Contracts.Remote.CommandStreamRecoveryRequest;

namespace GamePlatform.Sync.Recovery
{

    /// <summary>
    /// Account lifetime owned by the host. Quiescence drains gameplay admission, push, pull
    /// and late callbacks before returning true. New recovery requires verified damage;
    /// journal resume must fence writers even when the directory already names the successor.
    /// </summary>
    public interface ICommandStreamRecoveryHost
    {
        bool IsCurrentAccount { get; }
        Task<bool> QuiesceAsync(ClientStreamId priorStreamId, CommandStreamRecoveryReason reason, bool resume, CancellationToken cancellationToken);
        /// <summary>
        /// Opens successor storage without copying sequenced commands and performs genuine
        /// complete private bootstrap. The prior database/evidence remains quarantined.
        /// True requires durable identity, allocator, projections and checkpoint convergence;
        /// no gameplay admission occurs until this coordinator returns BootstrapComplete.
        /// </summary>
        Task<bool> BootstrapSuccessorAsync(ClientStreamId successorStreamId, CancellationToken cancellationToken);
    }
}
