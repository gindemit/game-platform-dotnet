using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Store;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;
namespace GamePlatform.Tests.Transport
{
    public sealed class StorefrontHttpProviderTests
    {
        [Fact]
        public async Task DisabledAndUnavailableCatalogueErrorsAreTyped()
        {
            foreach (var pair in new[] { (501, "store_feature_unavailable", RemoteFailureKind.Unavailable), (503, "store_catalog_unavailable", RemoteFailureKind.Dependency) })
            {
                using var refresh = new AuthRefreshCoordinator();
                var bytes = new MessagePackWireCodec().Encode(new ErrorResponse("test", new CommonError(pair.Item2, "dependency", "private", true, default, default), 1));
                var executor = new ErrorExecutor(pair.Item1, bytes);
                var provider = new StorefrontHttpProvider(new AppId(Guid.Parse("0199f9a0-0000-7000-8000-000000000001")),
                    new PlatformUserId(Guid.Parse("0199f9a0-0000-7000-8000-000000000002")),
                    new BackendHttpConfiguration(new Uri("https://unit.invalid/"), new BackendNamespace("test")), executor, new Auth(), refresh);
                var error = await Assert.ThrowsAsync<StorefrontOperationException>(() => provider.FetchAsync(CancellationToken.None));
                Assert.Equal(pair.Item3, error.Failure.Kind);
                Assert.Equal(pair.Item2, error.ResultCode);
                Assert.DoesNotContain("private", error.Message);
            }
        }

        private sealed class ErrorExecutor : IHttpExecutor
        {
            private readonly int status;
            private readonly byte[] bytes;
            public ErrorExecutor(int status, byte[] bytes) { this.status = status; this.bytes = bytes; }
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken token) =>
                Task.FromResult(new HttpResponseData(status, new Dictionary<string, string>
                {
                    ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType,
                    ["X-Correlation-Id"] = "test"
                }, bytes));
        }

        [Fact]
        public async Task UsesAccountCompositionAndRefreshesWithSameMessagePackRequest()
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor();
            var provider = new ProductionBackendHttpProviders(new BackendHttpConfiguration(new Uri("https://unit.invalid/"), new BackendNamespace("test")),
                executor, new Auth(), refresh).ForAccount(new AppId(Guid.Parse("0199f9a0-0000-7000-8000-000000000001")),
                    new PlatformUserId(Guid.Parse("0199f9a0-0000-7000-8000-000000000002")), Guid.Parse("0199f9a0-0000-7000-8000-000000000003")).Storefront;
            var result = await provider.FetchAsync(CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value!);
            Assert.Equal(2, executor.Requests.Count);
            Assert.EndsWith("/store/offers", executor.Requests[0].Uri);
            Assert.Equal(new byte[] { 0x80 }, executor.Requests[1].CopyBody());
            Assert.Equal("Bearer fresh", executor.Requests[1].Headers["Authorization"]);
        }
        private sealed class Executor : IHttpExecutor
        {
            public readonly List<HttpRequestData> Requests = new List<HttpRequestData>();
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken token)
            {
                Requests.Add(request);
                if (Requests.Count == 1)
                    return Task.FromResult(new HttpResponseData(401, new Dictionary<string, string>(), Array.Empty<byte>()));
                return Task.FromResult(new HttpResponseData(200, new Dictionary<string, string>
                {
                    ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType,
                    ["X-Correlation-Id"] = "test"
                }, new byte[] { 0x81, 0xa6, 0x6f, 0x66, 0x66, 0x65, 0x72, 0x73, 0x90 }));
            }
        }
        private sealed class Auth : IAuthSession
        {
            public string SessionKey => "test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("old", 1));
            public Task<AccessTokenSnapshot> RefreshAsync(long generation, CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("fresh", 2));
        }
    }
}
