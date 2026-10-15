using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
namespace GamePlatform.Features.Daily
{
    public interface IDailyRemote
    {
        Task<RemoteResult<DailyStatus>> StatusAsync(CancellationToken cancellationToken);
    }
}
