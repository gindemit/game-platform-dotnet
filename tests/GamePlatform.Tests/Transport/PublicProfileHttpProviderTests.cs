using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Profiles;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Tests.Features.Profiles;
using GamePlatform.Tests.Features.Teams;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Tests.Transport
{
    public sealed class PublicProfileHttpProviderTests
    {
        [Fact]
        public async Task PostsSubjectToPublicQueryRouteAndDecodesUnavailableSubject()
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor(_ => Reply(PublicProfileServiceTests.Response(available: false)));
            var result = await Provider(executor, refresh).QueryAsync(new PublicProfileQuery(PublicProfileServiceTests.Subject), CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value!.Profile);
            Assert.Equal("https://unit.invalid/v1/apps/" + TeamsServiceTests.App.ToString("D") + "/profiles/public/query", executor.Requests[0].Uri);
            Assert.Equal(PublicProfileServiceTests.Subject, new PublicProfileMessagePackCodec().DecodeQuery(executor.Requests[0].CopyBody()));
        }

        [Fact]
        public async Task AvailableSubjectKeepsTeamSummary()
        {
            using var refresh = new AuthRefreshCoordinator();
            var result = await Provider(new Executor(_ => Reply(PublicProfileServiceTests.Response())), refresh).QueryAsync(new PublicProfileQuery(PublicProfileServiceTests.Subject), CancellationToken.None);
            Assert.Equal("daisy", result.Value!.Profile!.Team!.BadgeId);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ForeignEchoFailsClosed(bool foreignSubject)
        {
            using var refresh = new AuthRefreshCoordinator();
            var reply = foreignSubject ? PublicProfileServiceTests.Response(PublicProfileServiceTests.OtherSubject) : PublicProfileServiceTests.Response(app: TeamsServiceTests.Operation);
            var result = await Provider(new Executor(_ => Reply(reply)), refresh).QueryAsync(new PublicProfileQuery(PublicProfileServiceTests.Subject), CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
        }

        [Fact]
        public async Task ForbiddenReadSurfacesAuthorizationWithClosedCode()
        {
            using var refresh = new AuthRefreshCoordinator();
            var bytes = new MessagePackWireCodec().Encode(new ErrorResponse("profile-test", new CommonError("profiles.forbidden", "authorization", "private diagnostic text", false, default, default), 1_790_000_000_000));
            var executor = new Executor(_ => new HttpResponseData(403, Headers(), bytes));
            var error = await Assert.ThrowsAsync<PublicProfileOperationException>(() => Provider(executor, refresh).QueryAsync(new PublicProfileQuery(PublicProfileServiceTests.Subject), CancellationToken.None));
            Assert.Equal(RemoteFailureKind.Authorization, error.Failure.Kind);
            Assert.Equal("profiles.forbidden", error.ResultCode);
        }

        [Fact]
        public async Task TransportFailureIsARetryableReadFailure()
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor(_ => throw new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "test"));
            Assert.Equal(RemoteFailureKind.Dependency, (await Provider(executor, refresh).QueryAsync(new PublicProfileQuery(PublicProfileServiceTests.Subject), CancellationToken.None)).Failure.Kind);
        }

        private static PublicProfileHttpProvider Provider(IHttpExecutor executor, AuthRefreshCoordinator refresh) =>
            new ProductionBackendHttpProviders(new BackendHttpConfiguration(new Uri("https://unit.invalid/"), new BackendNamespace("profile-test")), executor, new Auth(), refresh)
                .ForAccount(new AppId(TeamsServiceTests.App), new PlatformUserId(TeamsServiceTests.Account), TeamsServiceTests.Player).PublicProfiles;
        private static Dictionary<string, string> Headers() => new Dictionary<string, string>
        {
            ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType,
            ["X-Correlation-Id"] = "profile-test"
        };
        private static HttpResponseData Reply(PublicProfileResponse value) => new HttpResponseData(200, Headers(), new PublicProfileStateCodec().EncodeResponse(value));
        private sealed class Executor : IHttpExecutor
        {
            private readonly Func<HttpRequestData, HttpResponseData> response;
            public List<HttpRequestData> Requests = new List<HttpRequestData>();
            public Executor(Func<HttpRequestData, HttpResponseData> response) => this.response = response;
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken token)
            {
                Requests.Add(request);
                return Task.FromResult(response(request));
            }
        }
        private sealed class Auth : IAuthSession
        {
            public string SessionKey => "profile-test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("test-token", 1));
            public Task<AccessTokenSnapshot> RefreshAsync(long generation, CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("fresh-test-token", 2));
        }
    }
}
