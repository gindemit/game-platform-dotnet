using System;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Storage.Abstractions.Outbox
{

    public interface IAtomicCommandStore
    {
        Task<CommandAdmission> CommitAsync(
            string businessRunId,
            CommandDraft command,
            Action<ILocalStorageTransaction, long> applyProjection,
            CancellationToken cancellationToken);
    }
}
