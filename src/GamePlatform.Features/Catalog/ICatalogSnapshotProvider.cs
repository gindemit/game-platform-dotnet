using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Features.Contracts.Catalog;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Shared;

namespace GamePlatform.Features.Catalog
{
    /// <summary>Inbound provider seam only; no catalog wire route is implied by this contract.</summary>
    public interface ICatalogSnapshotProvider
    {
        Task<CatalogSnapshot> DownloadAsync(CatalogSnapshotRequest request, CancellationToken cancellationToken);
    }
}
