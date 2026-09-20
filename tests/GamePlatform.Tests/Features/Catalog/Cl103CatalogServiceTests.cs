#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Catalog;
using GamePlatform.Features.Contracts.Catalog;
using GamePlatform.Features.Contracts.Snapshots;

namespace GamePlatform.Tests.Features.Catalog
{
    public sealed class Cl103CatalogServiceTests
    {
        private static readonly Guid AppA = Guid.Parse("0199f9a0-1000-7000-8000-000000000001");
        private static readonly Guid AppB = Guid.Parse("0199f9a0-1000-7000-8000-000000000002");
        private static readonly Guid Player = Guid.Parse("0199f9a0-1000-7000-8000-000000000003");

        [Fact]
        public async Task SharedDefinitionKeepsPerAppPresentationAndAllCacheDimensionsIsolated()
        {
            var store = new TestStore();
            var provider = new TestProvider(request => request.Owner.Owner.AppId.Value == AppA
                ? Snapshot(request, 5, Entry("shared", "coin.shared", "definition", "a-label"), Entry("a-only", "coin.a", "a-definition", "a-only"))
                : Snapshot(request, 8, Entry("shared", "coin.shared", "definition", "b-label")));
            using var service = Service(provider, store);
            var appA = Request("app-a", "player", "view", 1, 1, "en", "adult");
            var appB = Request("app-b", "player", "view", 1, 1, "en", "adult");
            var appAOtherLocale = Request("app-a", "player", "view", 1, 1, "fr", "adult");
            var appAOtherAudience = Request("app-a", "player", "view", 1, 1, "en", "teen");

            var first = await service.RefreshAsync(appA, CancellationToken.None);
            var second = await service.RefreshAsync(appB, CancellationToken.None);
            await service.RefreshAsync(appAOtherLocale, CancellationToken.None);
            await service.RefreshAsync(appAOtherAudience, CancellationToken.None);

            Assert.Equal(FeatureSnapshotState.Available, first.State);
            Assert.True(first.Value!.Entries.Count == 2);
            Assert.True(second.Value!.Entries.Count == 1);
            Assert.Equal("definition", System.Text.Encoding.UTF8.GetString(first.Value.Entries[0].Definition.CopyDefinitionPayload()));
            Assert.Equal("a-label", System.Text.Encoding.UTF8.GetString(first.Value.Entries[0].Binding.CopyPresentationPayload()));
            Assert.Equal("b-label", System.Text.Encoding.UTF8.GetString(second.Value.Entries[0].Binding.CopyPresentationPayload()));
            Assert.Equal(4, provider.Calls);
            Assert.Equal(4, store.Installed.Count);
        }

        [Fact]
        public async Task RevokedBindingRequiresNewEmptyVersionWhileOfflinePriorVersionStaysStale()
        {
            var store = new TestStore();
            var provider = new TestProvider(request => request.SnapshotVersion == 1
                ? Snapshot(request, 1, Entry("coin", "test.coin", "definition", "visible"))
                : Snapshot(request, 2));
            using var service = Service(provider, store);
            var prior = Request("app", "player", "view", 4, 1, "en", "adult");
            var revoked = Request("app", "player", "view", 4, 2, "en", "adult");

            await service.RefreshAsync(prior, CancellationToken.None);
            var revokedSnapshot = await service.RefreshAsync(revoked, CancellationToken.None);
            var offlinePrior = await service.ReadCachedAsync(prior, CancellationToken.None);

            Assert.Empty(revokedSnapshot.Value!.Entries);
            Assert.Equal(FeatureSnapshotState.Stale, offlinePrior.State);
            Assert.Single(offlinePrior.Value!.Entries);
            Assert.Equal(2, provider.Calls);
        }

        [Fact]
        public async Task UnsupportedAndCorruptDownloadsFailClosedWithoutInstalling()
        {
            var store = new TestStore();
            var unsupported = new TestProvider(_ => throw new CatalogSnapshotUnsupportedVersionException("snapshot-schema", 2));
            using (var service = Service(unsupported, store))
            {
                var result = await service.RefreshAsync(Request("app", "player", "view", 1, 1, "en", "adult"), CancellationToken.None);
                Assert.Equal(FeatureSnapshotState.Unavailable, result.State);
                Assert.Equal("catalog_unsupported_version", result.DiagnosticCode);
            }

            var corrupt = new TestProvider(request => Snapshot(Request("other-app", "player", "view", 1, request.SnapshotVersion, "en", "adult"), 3));
            using (var service = Service(corrupt, store))
            {
                var result = await service.RefreshAsync(Request("app", "player", "view", 1, 2, "en", "adult"), CancellationToken.None);
                Assert.Equal(FeatureSnapshotState.Error, result.State);
                Assert.Equal("catalog_snapshot_corrupt", result.DiagnosticCode);
            }
            Assert.Empty(store.Installed);
        }

        [Fact]
        public async Task IncompleteDuplicateAndCorruptCacheCannotMasqueradeAsAllowedCatalog()
        {
            var request = Request("app", "player", "view", 1, 1, "en", "adult");
            Assert.Throws<CatalogSnapshotCorruptException>(() => Snapshot(request, 1,
                Entry("same", "one", "a", "a"), Entry("same", "two", "b", "b")));
            Assert.Throws<CatalogSnapshotCorruptException>(() =>
                new CatalogSnapshot(request, 1, 1, 100, new CatalogEntry[] { null! }));
            Assert.Throws<CatalogSnapshotUnsupportedVersionException>(() =>
                new CatalogDefinition(new PlatformId("definition"), CatalogResourceKind.Currency, new SemanticId("coin"), 1, 2, Array.Empty<byte>()));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CatalogDefinition(new PlatformId("definition"), CatalogResourceKind.Currency, new SemanticId("coin"), 1, 1, new byte[16 * 1024 + 1]));

            var store = new TestStore { ReadFailure = new CatalogSnapshotCorruptException("stored bytes are truncated") };
            using var service = Service(new TestProvider(_ => Snapshot(request, 1)), store);
            var cached = await service.ReadCachedAsync(request, CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Error, cached.State);
            Assert.Equal("catalog_cache_corrupt", cached.DiagnosticCode);
        }

        [Fact]
        public void VisibilityNeverConfersGrantSpendUseOrSubmitPermission()
        {
            var visibleCurrency = new CatalogAppBinding(new PlatformId("currency"), 1, true, false, false, false, false, Array.Empty<byte>());
            Assert.True(visibleCurrency.Permits(CatalogResourceKind.Currency, CatalogPermission.Visible));
            Assert.False(visibleCurrency.Permits(CatalogResourceKind.Currency, CatalogPermission.Grant));
            Assert.False(visibleCurrency.Permits(CatalogResourceKind.Currency, CatalogPermission.Spend));
            Assert.False(visibleCurrency.Permits(CatalogResourceKind.Currency, CatalogPermission.Use));
            Assert.False(visibleCurrency.Permits(CatalogResourceKind.Currency, CatalogPermission.Submit));
        }

        private static CatalogService Service(ICatalogSnapshotProvider provider, ICatalogSnapshotStore store) =>
            new CatalogService(provider, store, 16, TimeSpan.FromMinutes(1), () => 1_000);

        private static CatalogSnapshotRequest Request(string app, string player, string view, long generation, long version, string locale, string audience) =>
            new CatalogSnapshotRequest(new ScopedOwnerContext(new OwnerScope(new BackendNamespace("backend"), new AppId(app == "app-b" || app == "other-app" ? AppB : AppA),
                new PlatformUserId(Player)), new SemanticId(view), generation), version, locale, audience);

        private static CatalogEntry Entry(string definitionId, string key, string definitionPayload, string presentationPayload) =>
            new CatalogEntry(new CatalogDefinition(new PlatformId(definitionId), CatalogResourceKind.Currency, new SemanticId(key), 1, 1,
                System.Text.Encoding.UTF8.GetBytes(definitionPayload)), new CatalogAppBinding(new PlatformId(definitionId), 1, true,
                false, false, false, false, System.Text.Encoding.UTF8.GetBytes(presentationPayload)));

        private static CatalogSnapshot Snapshot(CatalogSnapshotRequest request, long revision, params CatalogEntry[] entries) =>
            new CatalogSnapshot(request, 1, revision, 100, entries);

        private sealed class TestProvider : ICatalogSnapshotProvider
        {
            private readonly Func<CatalogSnapshotRequest, CatalogSnapshot> download;
            public TestProvider(Func<CatalogSnapshotRequest, CatalogSnapshot> download) { this.download = download; }
            public int Calls { get; private set; }
            public Task<CatalogSnapshot> DownloadAsync(CatalogSnapshotRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested(); Calls++; return Task.FromResult(download(request));
            }
        }

        private sealed class TestStore : ICatalogSnapshotStore
        {
            private readonly Dictionary<string, CatalogSnapshot> snapshots = new Dictionary<string, CatalogSnapshot>();
            public List<CatalogSnapshot> Installed { get; } = new List<CatalogSnapshot>();
            public Exception? ReadFailure { get; set; }
            public Task<CatalogSnapshot?> ReadAsync(CatalogSnapshotRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ReadFailure != null) throw ReadFailure;
                snapshots.TryGetValue(Key(request), out var snapshot); return Task.FromResult<CatalogSnapshot?>(snapshot);
            }
            public Task InstallAsync(CatalogSnapshot snapshot, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                snapshots[Key(snapshot.Request)] = snapshot; Installed.Add(snapshot); return Task.CompletedTask;
            }
            private static string Key(CatalogSnapshotRequest request) => request.Owner.Owner.AppId.Value.ToString("D") + "/" + request.Owner.Owner.UserId.Value.ToString("D") + "/" + request.Owner.ViewKey.Value + "/" + request.Owner.Generation + "/" + request.ToCanonicalQuery();
        }
    }
}
