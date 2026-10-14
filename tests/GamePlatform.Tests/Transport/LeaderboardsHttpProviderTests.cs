using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Leaderboards;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Tests.Features.Leaderboards;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Tests.Transport
{
    public sealed class LeaderboardsHttpProviderTests
    {
        [Fact]
        public async Task AuthenticationRefreshReusesExactMutationBytesAndIdentity()
        {
            using var refresh = new AuthRefreshCoordinator();
            var auth = new Auth();
            var executor = new Executor((request, count) => count == 1 ? new HttpResponseData(401, new Dictionary<string, string>(), Array.Empty<byte>()) : Reply(LeaderboardsServiceTests.CountryResponse()));
            var provider = Provider(executor, auth, refresh);
            var result = await provider.SetCountryAsync(LeaderboardsServiceTests.Command(), CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(1, auth.Refreshes);
            Assert.Equal(executor.Requests[0].CopyBody(), executor.Requests[1].CopyBody());
            Assert.EndsWith("/leaderboards/country", executor.Requests[0].Uri);
            Assert.Equal("Bearer fresh-test-token", executor.Requests[1].Headers["Authorization"]);
        }

        [Theory]
        [InlineData(HttpDeliveryCertainty.NotSent, RemoteFailureKind.NotSent)]
        [InlineData(HttpDeliveryCertainty.Uncertain, RemoteFailureKind.OutcomeUncertain)]
        public async Task MutationTransportFailurePreservesDeliveryCertainty(HttpDeliveryCertainty certainty, RemoteFailureKind expected)
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor((_, __) => throw new HttpExecutionException(certainty, "test"));
            Assert.Equal(expected, (await Provider(executor, new Auth(), refresh).SetCountryAsync(LeaderboardsServiceTests.Command(), CancellationToken.None)).Failure.Kind);
        }

        [Fact]
        public async Task ForeignOperationFailsClosed()
        {
            using var refresh = new AuthRefreshCoordinator();
            var response = LeaderboardsServiceTests.CountryResponse(LeaderboardsServiceTests.Command(operation: GamePlatform.Tests.Features.Teams.TeamsServiceTests.Team));
            var provider = Provider(new Executor((_, __) => Reply(response)), new Auth(), refresh);
            Assert.Equal(RemoteFailureKind.Protocol, (await provider.SetCountryAsync(LeaderboardsServiceTests.Command(), CancellationToken.None)).Failure.Kind);
        }

        [Fact]
        public async Task ClosedErrorCodeAndRetryDelayAreExposedWithoutPrivateMessage()
        {
            using var refresh = new AuthRefreshCoordinator();
            var bytes = new MessagePackWireCodec().Encode(new ErrorResponse("leaderboards-test", new CommonError("leaderboards.rate_limited", "rate_limit", "private diagnostic text", true, default, default), 1_790_000_000_000));
            var executor = new Executor((_, __) => new HttpResponseData(429, new Dictionary<string, string>
            {
                ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType,
                ["X-Correlation-Id"] = "leaderboards-test",
                ["Retry-After"] = "7"
            }, bytes));
            var error = await Assert.ThrowsAsync<LeaderboardsOperationException>(() => Provider(executor, new Auth(), refresh).QueryAsync(new LeaderboardsQuery("players-monthly"), CancellationToken.None));
            Assert.Equal("leaderboards.rate_limited", error.ResultCode);
            Assert.Equal(TimeSpan.FromSeconds(7), error.Failure.RetryAfter);
            Assert.DoesNotContain("private diagnostic", error.Message);
        }

        [Fact]
        public async Task JsonResponseIsRejectedWithoutFallback()
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor((_, __) => new HttpResponseData(200, new Dictionary<string, string> { ["Content-Type"] = "application/json", ["X-Correlation-Id"] = "leaderboards-test" }, new byte[] { 123, 125 }));
            Assert.Equal(RemoteFailureKind.Protocol, (await Provider(executor, new Auth(), refresh).QueryAsync(new LeaderboardsQuery("players-monthly"), CancellationToken.None)).Failure.Kind);
        }

        [Fact]
        public async Task MalformedErrorCannotBecomeATerminalMutationRejection()
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor((_, __) => new HttpResponseData(409, new Dictionary<string, string> { ["Content-Type"] = "text/html" }, new byte[] { 0 }));
            var error = await Assert.ThrowsAsync<LeaderboardsOperationException>(() => Provider(executor, new Auth(), refresh).SetCountryAsync(LeaderboardsServiceTests.Command(), CancellationToken.None));
            Assert.Equal(RemoteFailureKind.Protocol, error.Failure.Kind);
        }

        private static LeaderboardsHttpProvider Provider(IHttpExecutor executor, IAuthSession auth, AuthRefreshCoordinator refresh) =>
            new ProductionBackendHttpProviders(new BackendHttpConfiguration(new Uri("https://unit.invalid/"), new BackendNamespace("leaderboards-test")), executor, auth, refresh)
                .ForAccount(new AppId(GamePlatform.Tests.Features.Teams.TeamsServiceTests.App), new PlatformUserId(GamePlatform.Tests.Features.Teams.TeamsServiceTests.Account), GamePlatform.Tests.Features.Teams.TeamsServiceTests.Player).Leaderboards;
        private static HttpResponseData Reply(LeaderboardsCountryResponse value) => new HttpResponseData(200, new Dictionary<string, string>
        {
            ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType,
            ["X-Correlation-Id"] = "leaderboards-test"
        }, new LeaderboardsMessagePackCodec().EncodeCountryResponse(new GamePlatform.Wire.Contracts.Leaderboards.LeaderboardsCountryResponse(value.OperationId, value.BoardId, value.ParticipantKind, value.ParticipantId, value.CountryCode, value.EffectiveAt)));
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
            public string SessionKey => "leaderboards-test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("old-test-token", 1));
            public Task<AccessTokenSnapshot> RefreshAsync(long generation, CancellationToken token)
            {
                Refreshes++;
                return Task.FromResult(new AccessTokenSnapshot("fresh-test-token", 2));
            }
        }
    }
}
