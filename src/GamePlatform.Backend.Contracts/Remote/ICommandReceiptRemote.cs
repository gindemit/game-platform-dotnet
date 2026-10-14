using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public interface ICommandReceiptRemote
    {
        Task<RemoteResult<RemoteCommandReceipt>> LookupAsync(RemoteCommand command, CancellationToken cancellationToken);
    }
}
