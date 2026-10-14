using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public interface ICommandStreamRecoveryRemote
    {
        Task<RemoteResult<CommandStreamInspection>> InspectAsync(ClientStreamId streamId, CancellationToken cancellationToken);
        Task<RemoteResult<CommandStreamRecoveryResult>> RecoverAsync(CommandStreamRecoveryRequest request, CancellationToken cancellationToken);
    }
}
