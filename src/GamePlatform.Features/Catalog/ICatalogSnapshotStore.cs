using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Features.Contracts.Catalog;

namespace GamePlatform.Features.Catalog
{

    /// <summary>Feature-owned durable projection seam. Production implementations must use real storage.</summary>
    public interface ICatalogSnapshotStore
    {
        Task<CatalogSnapshot?> ReadAsync(CatalogSnapshotRequest request, CancellationToken cancellationToken);
        Task InstallAsync(CatalogSnapshot snapshot, CancellationToken cancellationToken);
    }
}
