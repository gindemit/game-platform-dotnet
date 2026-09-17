using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Sync
{
    public sealed class UnavailableSyncCoordinator
    {
        public Task<PlatformResult<string>> RunOnceAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(PlatformResult<string>.Error(PlatformFailure.NotImplemented, "Sync is not implemented in M0."));
        }
    }
}
