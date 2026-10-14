using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
namespace GamePlatform.Features.Leaderboards
{
    public interface ILeaderboardsRemote
    {
        Task<RemoteResult<LeaderboardsResponse>> QueryAsync(LeaderboardsQuery query, CancellationToken cancellationToken);
        Task<RemoteResult<LeaderboardsCountryResponse>> SetCountryAsync(LeaderboardsCountryCommand command, CancellationToken cancellationToken);
    }
}
