using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Navigation
{

    public interface IRouteLoader
    {
        Task<RouteLoadResult> LoadAsync(NavigationRoute route, CancellationToken cancellationToken);
    }
}
