using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace GamePlatform.Features.Purchases
{
    public interface INativeStoreBridge
    {
        Task ConnectAsync(CancellationToken cancellationToken);
        Task<IReadOnlyList<NativeStoreProduct>> QueryProductsAsync(IReadOnlyList<NativeProduct> products, CancellationToken cancellationToken);
        Task<NativeLaunchResult> LaunchAsync(NativeLaunchRequest request, CancellationToken cancellationToken);
        Task<NativeProofScanResult> QueryUnfinishedAsync(CancellationToken cancellationToken);
    }
}
