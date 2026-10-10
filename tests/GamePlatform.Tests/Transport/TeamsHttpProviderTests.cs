#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Teams;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Tests.Features.Teams;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Tests.Transport
{
    public sealed class TeamsHttpProviderTests
    {
        [Fact]
        public async Task AuthenticationRefreshReusesExactMutationBytesAndIdentity()
        {
            using var refresh = new AuthRefreshCoordinator();
            var auth = new Auth();
            var executor = new Executor((request, count) => count == 1 ? new HttpResponseData(401, new Dictionary<string, string>(), Array.Empty<byte>()) : Reply(TeamsServiceTests.Response(TeamsServiceTests.Operation)));
            var provider = Provider(executor, auth, refresh);
            var result = await provider.ExecuteAsync(TeamsServiceTests.Command(), CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(1, auth.Refreshes);
            Assert.Equal(executor.Requests[0].CopyBody(), executor.Requests[1].CopyBody());
            Assert.EndsWith("/teams/commands", executor.Requests[0].Uri);
            Assert.Equal("Bearer fresh-test-token", executor.Requests[1].Headers["Authorization"]);
        }

        [Theory]
        [InlineData(HttpDeliveryCertainty.NotSent, RemoteFailureKind.NotSent)]
        [InlineData(HttpDeliveryCertainty.Uncertain, RemoteFailureKind.OutcomeUncertain)]
        public async Task MutationTransportFailurePreservesDeliveryCertainty(HttpDeliveryCertainty certainty, RemoteFailureKind expected)
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor((_, __) => throw new HttpExecutionException(certainty, "test"));
            Assert.Equal(expected, (await Provider(executor, new Auth(), refresh).ExecuteAsync(TeamsServiceTests.Command(), CancellationToken.None)).Failure.Kind);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ForeignAccountOrOperationFailsClosed(bool foreignAccount)
        {
            using var refresh = new AuthRefreshCoordinator();
            var response = foreignAccount ? TeamsServiceTests.Response(TeamsServiceTests.Operation, account: TeamsServiceTests.Team) : TeamsServiceTests.Response(TeamsServiceTests.Team);
            var provider = Provider(new Executor((_, __) => Reply(response)), new Auth(), refresh);
            Assert.Equal(RemoteFailureKind.Protocol, (await provider.ExecuteAsync(TeamsServiceTests.Command(), CancellationToken.None)).Failure.Kind);
        }

        [Fact]
        public async Task ClosedErrorCodeAndRetryDelayAreExposedWithoutPrivateMessage()
        {
            using var refresh = new AuthRefreshCoordinator();
            var bytes = new MessagePackWireCodec().Encode(new ErrorResponse("teams-test", new CommonError("teams.rate_limited", "rate_limit", "private diagnostic text", true, default, default), 1_790_000_000_000));
            var executor = new Executor((_, __) => new HttpResponseData(429, new Dictionary<string, string>
            { ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType, ["X-Correlation-Id"] = "teams-test", ["Retry-After"] = "7" }, bytes));
            var error = await Assert.ThrowsAsync<TeamsOperationException>(() => Provider(executor, new Auth(), refresh).QueryAsync(new TeamsQuery("mine"), CancellationToken.None));
            Assert.Equal("teams.rate_limited", error.ResultCode);
            Assert.Equal(TimeSpan.FromSeconds(7), error.Failure.RetryAfter);
            Assert.DoesNotContain("private diagnostic", error.Message);
        }

        [Fact]
        public async Task JsonResponseIsRejectedWithoutFallback()
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor((_, __) => new HttpResponseData(200, new Dictionary<string, string> { ["Content-Type"] = "application/json", ["X-Correlation-Id"] = "teams-test" }, new byte[] { 123, 125 }));
            Assert.Equal(RemoteFailureKind.Protocol, (await Provider(executor, new Auth(), refresh).QueryAsync(new TeamsQuery("mine"), CancellationToken.None)).Failure.Kind);
        }

        [Fact]
        public async Task MalformedErrorCannotBecomeATerminalMutationRejection()
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor((_, __) => new HttpResponseData(409, new Dictionary<string, string> { ["Content-Type"] = "text/html" }, new byte[] { 0 }));
            var error = await Assert.ThrowsAsync<TeamsOperationException>(() => Provider(executor, new Auth(), refresh).ExecuteAsync(TeamsServiceTests.Command(), CancellationToken.None));
            Assert.Equal(RemoteFailureKind.Protocol, error.Failure.Kind);
        }

        private static TeamsHttpProvider Provider(IHttpExecutor executor, IAuthSession auth, AuthRefreshCoordinator refresh) =>
            new ProductionBackendHttpProviders(new BackendHttpConfiguration(new Uri("https://unit.invalid/"), new BackendNamespace("teams-test")), executor, auth, refresh)
                .ForAccount(new AppId(TeamsServiceTests.App), new PlatformUserId(TeamsServiceTests.Account), TeamsServiceTests.Player).Teams;
        private static HttpResponseData Reply(TeamsResponse value) => new HttpResponseData(200, new Dictionary<string, string>
        { ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType, ["X-Correlation-Id"] = "teams-test" }, new TeamsStateCodec().EncodeResponse(value));
        private sealed class Executor : IHttpExecutor
        {
            private readonly Func<HttpRequestData, int, HttpResponseData> response;
            public List<HttpRequestData> Requests = new List<HttpRequestData>();
            public Executor(Func<HttpRequestData, int, HttpResponseData> response) => this.response = response;
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken token) { Requests.Add(request); return Task.FromResult(response(request, Requests.Count)); }
        }
        private sealed class Auth : IAuthSession
        {
            public int Refreshes;
            public string SessionKey => "teams-test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("old-test-token", 1));
            public Task<AccessTokenSnapshot> RefreshAsync(long generation, CancellationToken token) { Refreshes++; return Task.FromResult(new AccessTokenSnapshot("fresh-test-token", 2)); }
        }
    }
}
