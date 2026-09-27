#nullable enable
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

    /// <summary>Feature-owned durable projection seam. Production implementations must use real storage.</summary>
    public interface ICatalogSnapshotStore
    {
        Task<CatalogSnapshot?> ReadAsync(CatalogSnapshotRequest request, CancellationToken cancellationToken);
        Task InstallAsync(CatalogSnapshot snapshot, CancellationToken cancellationToken);
    }

    /// <summary>Validated catalog reader. Visibility is presentation eligibility only, never a value mutation authorization.</summary>
    public sealed class CatalogService : IDisposable
    {
        private const string FeatureNamespace = "catalog.snapshot";
        private readonly ICatalogSnapshotProvider provider;
        private readonly ICatalogSnapshotStore store;
        private readonly DisposableQueryCache<CatalogSnapshot> refreshCache;

        public CatalogService(ICatalogSnapshotProvider provider, ICatalogSnapshotStore store,
            int cacheCapacity, TimeSpan cacheLifetime, Func<long> nowMilliseconds)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            refreshCache = new DisposableQueryCache<CatalogSnapshot>(cacheCapacity, cacheLifetime,
                nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds)));
        }

        public async Task<FeatureSnapshot<CatalogSnapshot>> ReadCachedAsync(CatalogSnapshotRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var snapshot = await store.ReadAsync(request, cancellationToken).ConfigureAwait(false);
                if (snapshot == null) return new FeatureSnapshot<CatalogSnapshot>(request.Owner, 0, FeatureSnapshotState.Missing,
                    SnapshotFreshness.Missing, null, null, null);
                ValidateMatchesRequest(snapshot, request);
                return new FeatureSnapshot<CatalogSnapshot>(request.Owner, snapshot.Revision, FeatureSnapshotState.Stale,
                    SnapshotFreshness.Stale, snapshot, snapshot.ConfirmedAtMilliseconds, null);
            }
            catch (CatalogSnapshotUnsupportedVersionException)
            {
                return new FeatureSnapshot<CatalogSnapshot>(request.Owner, 0, FeatureSnapshotState.Unavailable,
                    SnapshotFreshness.Missing, null, null, "catalog_unsupported_version");
            }
            catch (CatalogSnapshotCorruptException)
            {
                return new FeatureSnapshot<CatalogSnapshot>(request.Owner, 0, FeatureSnapshotState.Error,
                    SnapshotFreshness.Missing, null, null, "catalog_cache_corrupt");
            }
        }

        public Task<FeatureSnapshot<CatalogSnapshot>> RefreshAsync(CatalogSnapshotRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var key = new ScopedQueryKey(request.Owner, FeatureNamespace, request.ToCanonicalQuery());
            return refreshCache.GetOrRefreshAsync(key, async lifetimeToken =>
            {
                try
                {
                    var snapshot = await provider.DownloadAsync(request, lifetimeToken).ConfigureAwait(false);
                    if (snapshot == null) throw new CatalogSnapshotCorruptException("The catalog provider returned no snapshot.");
                    ValidateMatchesRequest(snapshot, request);
                    await store.InstallAsync(snapshot, lifetimeToken).ConfigureAwait(false);
                    return new FeatureSnapshot<CatalogSnapshot>(request.Owner, snapshot.Revision, FeatureSnapshotState.Available,
                        SnapshotFreshness.Current, snapshot, snapshot.ConfirmedAtMilliseconds, null);
                }
                catch (CatalogSnapshotUnsupportedVersionException)
                {
                    return new FeatureSnapshot<CatalogSnapshot>(request.Owner, 0, FeatureSnapshotState.Unavailable,
                        SnapshotFreshness.Missing, null, null, "catalog_unsupported_version");
                }
                catch (CatalogSnapshotCorruptException)
                {
                    return new FeatureSnapshot<CatalogSnapshot>(request.Owner, 0, FeatureSnapshotState.Error,
                        SnapshotFreshness.Missing, null, null, "catalog_snapshot_corrupt");
                }
            }, cancellationToken);
        }

        public void Dispose() => refreshCache.Dispose();

        private static void ValidateMatchesRequest(CatalogSnapshot snapshot, CatalogSnapshotRequest request)
        {
            if (snapshot.Request.Owner != request.Owner || snapshot.Request.SnapshotVersion != request.SnapshotVersion ||
                !string.Equals(snapshot.Request.Locale, request.Locale, StringComparison.Ordinal) ||
                !string.Equals(snapshot.Request.Audience, request.Audience, StringComparison.Ordinal))
                throw new CatalogSnapshotCorruptException("A catalog snapshot escaped its cache identity.");
        }
    }
}
