using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Outbox
{

    public interface ICommandDeliveryStore
    {
        Task<LeasedCommand?> LeaseNextAsync(long nowMilliseconds, long leaseMilliseconds, CancellationToken cancellationToken);
        Task FinalizeAsync(OperationId operationId, long sequence, CommandTerminalOutcome outcome, byte[]? terminalResult, CancellationToken cancellationToken);
        Task ReleaseAsync(OperationId operationId, long sequence, CancellationToken cancellationToken);
    }
}
