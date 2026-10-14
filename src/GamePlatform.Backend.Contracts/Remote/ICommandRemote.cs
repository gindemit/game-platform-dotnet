using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{
    public interface ICommandRemote
    {
        Task<RemoteResult<RemoteCommandOutcome>> SendAsync(RemoteCommand command, CancellationToken cancellationToken);
    }
}
