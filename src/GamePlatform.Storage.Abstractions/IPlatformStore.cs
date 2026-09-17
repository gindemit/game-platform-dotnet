using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions
{
    public interface IPlatformStore
    {
        Task<PlatformResult<string>> OpenAccountAsync(PlatformId appId, PlatformId accountId, CancellationToken cancellationToken);
    }
}
