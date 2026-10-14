using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Transport.Abstractions
{

    public interface IHttpExecutor
    {
        Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken);
    }
}
