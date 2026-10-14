using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;

namespace GamePlatform.Features.Teams
{
    public interface ITeamsRemote
    {
        Task<RemoteResult<TeamsResponse>> QueryAsync(TeamsQuery query, CancellationToken cancellationToken);
        Task<RemoteResult<TeamsResponse>> ExecuteAsync(TeamsCommand command, CancellationToken cancellationToken);
    }
}
