using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Transport.Abstractions
{
    public interface IHttpExecutor { Task<byte[]> SendAsync(byte[] request, CancellationToken cancellationToken); }
    public interface IWireCodec { byte[] Encode<T>(T value); T Decode<T>(byte[] payload); }
}
