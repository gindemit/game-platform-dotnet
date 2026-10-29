using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;

namespace GamePlatform.Features.Store
{
    public interface IStorefrontRemote
    {
        Task<RemoteResult<IReadOnlyList<StorefrontOffer>>> FetchAsync(CancellationToken cancellationToken);
    }
}
