using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
namespace GamePlatform.Features.Friends
{
    public interface IFriendsRemote
    {
        Task<RemoteResult<FriendsResponse>> QueryAsync(FriendsQuery query, CancellationToken token);
        Task<RemoteResult<FriendsResponse>> ExecuteAsync(FriendsCommand command, CancellationToken token);
    }
}
