using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Backend.Contracts.Remote
{
    public interface ICommandRemote
    {
        Task<RemoteResult<RemoteCommandOutcome>> SendAsync(RemoteCommand command, CancellationToken cancellationToken);
    }
}
