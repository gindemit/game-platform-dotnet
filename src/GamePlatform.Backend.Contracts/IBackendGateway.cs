using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts
{
    public interface IBackendGateway
    {
        Task<PlatformResult<byte[]>> SendAsync(byte[] request, CancellationToken cancellationToken);
    }
}
