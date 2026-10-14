using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Features.Contracts.Catalog;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Shared;

namespace GamePlatform.Features.Catalog
{

    /// <summary>Feature-owned durable projection seam. Production implementations must use real storage.</summary>
    public interface ICatalogSnapshotStore
    {
        Task<CatalogSnapshot?> ReadAsync(CatalogSnapshotRequest request, CancellationToken cancellationToken);
        Task InstallAsync(CatalogSnapshot snapshot, CancellationToken cancellationToken);
    }
}
