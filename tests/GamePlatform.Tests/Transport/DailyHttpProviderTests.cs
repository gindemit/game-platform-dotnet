using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Daily;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Tests.Features.Daily;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;
namespace GamePlatform.Tests.Transport
{
    public sealed class DailyHttpProviderTests
    {
        [Fact]
        public async Task StatusPostsAnEmptyBodyAndRefreshesOnce()
        {
            using var refresh = new AuthRefreshCoordinator();
            var auth = new Auth();
            var executor = new Executor((_, count) => count == 1 ? new HttpResponseData(401, new Dictionary<string, string>(), Array.Empty<byte>()) : Reply());
            var result = await Provider(executor, auth, refresh).StatusAsync(CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(("2026-10-15", true, 3, 1_792_060_800_000), (result.Value!.UtcDate, result.Value.Eligible, result.Value.CycleDay, result.Value.ServerTimeMilliseconds));
            Assert.Equal(1, auth.Refreshes);
            Assert.EndsWith("/daily/status", executor.Requests[0].Uri);
            Assert.Equal(new byte[] { 0x80 }, executor.Requests[1].CopyBody());
            Assert.Equal("Bearer fresh-test-token", executor.Requests[1].Headers["Authorization"]);
        }
        [Fact]
        public async Task MissingRouteIsAnUnavailableCapability()
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor((_, __) => new HttpResponseData(404, new Dictionary<string, string> { ["Content-Type"] = "text/html" }, new byte[] { 0 }));
            var error = await Assert.ThrowsAsync<DailyOperationException>(() => Provider(executor, new Auth(), refresh).StatusAsync(CancellationToken.None));
            Assert.Equal(RemoteFailureKind.Unavailable, error.Failure.Kind);
            Assert.Equal("daily_unavailable", error.ResultCode);
        }
        [Fact]
        public async Task ClosedErrorCodeIsExposedWithoutPrivateMessage()
        {
            using var refresh = new AuthRefreshCoordinator();
            var bytes = new MessagePackWireCodec().Encode(new ErrorResponse("daily-test", new CommonError("daily_unavailable", "validation", "private diagnostic text", false, default, default), 1_790_000_000_000));
            var executor = new Executor((_, __) => new HttpResponseData(404, new Dictionary<string, string> { ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType, ["X-Correlation-Id"] = "daily-test" }, bytes));
            var error = await Assert.ThrowsAsync<DailyOperationException>(() => Provider(executor, new Auth(), refresh).StatusAsync(CancellationToken.None));
            Assert.Equal((RemoteFailureKind.Unavailable, "daily_unavailable"), (error.Failure.Kind, error.ResultCode));
            Assert.DoesNotContain("private diagnostic", error.Message);
        }
        [Fact]
        public async Task JsonResponseIsRejectedWithoutFallback()
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor((_, __) => new HttpResponseData(200, new Dictionary<string, string> { ["Content-Type"] = "application/json", ["X-Correlation-Id"] = "daily-test" }, new byte[] { 123, 125 }));
            Assert.Equal(RemoteFailureKind.Protocol, (await Provider(executor, new Auth(), refresh).StatusAsync(CancellationToken.None)).Failure.Kind);
        }
        private static DailyHttpProvider Provider(IHttpExecutor executor, IAuthSession auth, AuthRefreshCoordinator refresh) =>
            new ProductionBackendHttpProviders(new BackendHttpConfiguration(new Uri("https://unit.invalid/"), new BackendNamespace("daily-test")), executor, auth, refresh)
                .ForAccount(new AppId(DailyServiceTests.App), new PlatformUserId(DailyServiceTests.Account), DailyServiceTests.ClaimId).Daily;
        private static HttpResponseData Reply() => new HttpResponseData(200, new Dictionary<string, string>
        {
            ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType,
            ["X-Correlation-Id"] = "daily-test"
        }, new DailyFeatureCodec().EncodeStatus(DailyServiceTests.Status()));
        private sealed class Executor : IHttpExecutor
        {
            private readonly Func<HttpRequestData, int, HttpResponseData> response;
            public List<HttpRequestData> Requests = new List<HttpRequestData>();
            public Executor(Func<HttpRequestData, int, HttpResponseData> response) => this.response = response;
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken token)
            {
                Requests.Add(request);
                return Task.FromResult(response(request, Requests.Count));
            }
        }
        private sealed class Auth : IAuthSession
        {
            public int Refreshes;
            public string SessionKey => "daily-test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("old-test-token", 1));
            public Task<AccessTokenSnapshot> RefreshAsync(long generation, CancellationToken token)
            {
                Refreshes++;
                return Task.FromResult(new AccessTokenSnapshot("fresh-test-token", 2));
            }
        }
    }
}
