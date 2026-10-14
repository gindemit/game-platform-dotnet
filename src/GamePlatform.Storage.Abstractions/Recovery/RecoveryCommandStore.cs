using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Storage.Abstractions.Recovery
{
    /// <summary>
    /// Retained prior-stream evidence. The host must drain all prior writers before use.
    /// Suspension is durable; absent receipts never license deleting or moving intents.
    /// </summary>
    public interface IRecoveryCommandStore
    {
        Task<IReadOnlyList<LeasedCommand>> SuspendAndReadAsync(ClientStreamId priorStreamId, CancellationToken cancellationToken);
        Task RecordReceiptAsync(LeasedCommand command, CommandTerminalOutcome outcome, byte[]? result, CancellationToken cancellationToken);
        Task<bool> IsSafeToRetireAsync(ClientStreamId priorStreamId, CancellationToken cancellationToken);
    }
}
