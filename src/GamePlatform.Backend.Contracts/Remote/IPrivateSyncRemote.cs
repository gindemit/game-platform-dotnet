using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public interface IPrivateSyncRemote
    {
        Task<RemoteResult<BootstrapStart>> StartBootstrapAsync(ClientStreamId streamId, CancellationToken cancellationToken);
        Task<RemoteResult<BootstrapPage>> GetBootstrapPageAsync(byte[] session, byte[] pageToken, int maximumBytes, CancellationToken cancellationToken);
        Task<RemoteResult<RemotePullPage>> PullAsync(byte[] cursor, int maximumBytes, CancellationToken cancellationToken);
    }
}
