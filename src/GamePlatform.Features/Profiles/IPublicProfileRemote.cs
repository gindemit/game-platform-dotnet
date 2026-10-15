using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;

namespace GamePlatform.Features.Profiles
{
    public interface IPublicProfileRemote
    {
        Task<RemoteResult<PublicProfileResponse>> QueryAsync(PublicProfileQuery query, CancellationToken cancellationToken);
    }
}
