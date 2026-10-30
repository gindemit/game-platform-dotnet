using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
namespace GamePlatform.Storage.Abstractions.Sync
{
    public interface INativeInstalledCheckpointReader
    {
        Task<NativeInstalledCheckpoint?> ReadAsync(ScopedOwnerContext owner, CancellationToken token);
    }
}
