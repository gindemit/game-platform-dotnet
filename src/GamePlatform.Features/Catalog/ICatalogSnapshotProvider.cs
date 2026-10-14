using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Features.Contracts.Catalog;

namespace GamePlatform.Features.Catalog
{
    /// <summary>Inbound provider seam only; no catalog wire route is implied by this contract.</summary>
    public interface ICatalogSnapshotProvider
    {
        Task<CatalogSnapshot> DownloadAsync(CatalogSnapshotRequest request, CancellationToken cancellationToken);
    }
}
