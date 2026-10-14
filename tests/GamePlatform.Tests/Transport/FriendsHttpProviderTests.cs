using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Friends;
using GamePlatform.Features.Leaderboards;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Tests.Serialization;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
namespace GamePlatform.Tests.Transport
{
    public sealed class FriendsHttpProviderTests
    {
        private static readonly Guid Id = Guid.Parse("019a17aa-1000-7000-8000-000000000001");
        [Fact]
        public async Task ProductionFactoryRoutesFriendsAndZeroScoreRankings()
        {
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Executor((r, _) => Reply(r.Uri.EndsWith("/leaderboards/friends/query", StringComparison.Ordinal) ? Fixture("zero-self", "rankingQueryResponse") : Fixture("empty-owner", "response")));
            var providers = Providers(executor, new Auth(), refresh);
            Assert.True((await providers.Friends.QueryAsync(new FriendsQuery("mine"), CancellationToken.None)).IsSuccess);
            var ranks = await providers.Leaderboards.QueryAsync(new LeaderboardsQuery("players-monthly", "friends"), CancellationToken.None);
            Assert.True(ranks.IsSuccess);
            Assert.Equal(0, ranks.Value!.Self!.Score);
            Assert.EndsWith("/friends/query", executor.Requests[0].Uri);
            Assert.EndsWith("/leaderboards/friends/query", executor.Requests[1].Uri);
        }
        [Fact]
        public async Task AuthRefreshPreservesExactCommandBodyAndOperation()
        {
            using var refresh = new AuthRefreshCoordinator();
            var auth = new Auth();
            var value = Diagnostic("empty-owner");
            var fields = value.Properties.ToDictionary(x => x.Key, x => x.Value);
            fields["view"] = V.Null;
            fields["operationId"] = V.String(Id.ToString("D"));
            fields["resultCode"] = V.String("set_privacy");
            fields["allowRequests"] = V.Boolean(false);
            var bytes = new QualificationMessagePackCodec().Encode("friends.schema.json#/$defs/response", V.Object(fields));
            var executor = new Executor((_, n) => n == 1 ? new HttpResponseData(401, new Dictionary<string, string>(), Array.Empty<byte>()) : Reply(bytes));
            var result = await Providers(executor, auth, refresh).Friends.ExecuteAsync(new FriendsCommand(new OperationId(Id), "set_privacy", new FriendsCommandPayload(allowRequests: false)), CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(1, auth.Refreshes);
            Assert.Equal(executor.Requests[0].CopyBody(), executor.Requests[1].CopyBody());
            Assert.EndsWith("/friends/commands", executor.Requests[1].Uri);
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RetainedRejectionAttestationIsTypedButAdmissionCannotResolveIdentity(bool retained)
        {
            using var refresh = new AuthRefreshCoordinator();
            var details = retained ? WireOptional<IReadOnlyDictionary<string, ErrorDetailValue>>.Present(new Dictionary<string, ErrorDetailValue> { ["operationId"] = ErrorDetailValue.FromString(Id.ToString("D")), ["receiptRetained"] = ErrorDetailValue.FromBoolean(true), ["retryAfterMilliseconds"] = ErrorDetailValue.FromInteger(86400000) }) : default;
            var error = new ErrorResponse("friends-test", new CommonError(retained ? "friends.pair_cooldown" : "friends.admission_limited", "rate_limit", "private text", !retained, retained ? default : WireOptional<int>.Present(60000), details), 1791712800000);
            var executor = new Executor((_, __) => Reply(new MessagePackWireCodec().Encode(error), 429));
            var caught = await Assert.ThrowsAsync<FriendsOperationException>(() => Providers(executor, new Auth(), refresh).Friends.ExecuteAsync(new FriendsCommand(new OperationId(Id), "set_privacy", new FriendsCommandPayload(allowRequests: false)), CancellationToken.None));
            Assert.Equal(retained ? Id : (Guid?)null, caught.RetainedOperationId);
            Assert.Equal(retained ? 86400000 : 60000, caught.RetryAfterMilliseconds);
            Assert.DoesNotContain("private text", caught.Message);
        }
        [Fact]
        public async Task JsonForeignViewAndMalformedErrorsFailClosed()
        {
            using var refresh = new AuthRefreshCoordinator();
            var foreign = new Executor((_, __) => Reply(Fixture("empty-owner", "response")));
            Assert.Equal(RemoteFailureKind.Protocol, (await Providers(foreign, new Auth(), refresh).Friends.QueryAsync(new FriendsQuery("friends"), CancellationToken.None)).Failure.Kind);
            var invalid = new Executor((_, __) => new HttpResponseData(200, new Dictionary<string, string> { ["Content-Type"] = "application/json", ["X-Correlation-Id"] = "friends-test" }, new byte[] { 123, 125 }));
            Assert.Equal(RemoteFailureKind.Protocol, (await Providers(invalid, new Auth(), refresh).Friends.QueryAsync(new FriendsQuery("mine"), CancellationToken.None)).Failure.Kind);
            var malformed = new Executor((_, __) => new HttpResponseData(409, new Dictionary<string, string>(), new byte[] { 0 }));
            var error = await Assert.ThrowsAsync<FriendsOperationException>(() => Providers(malformed, new Auth(), refresh).Friends.QueryAsync(new FriendsQuery("mine"), CancellationToken.None));
            Assert.Equal("friends.protocol", error.ResultCode);
            Assert.Null(error.RetainedOperationId);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RealHttpBusinessRejectionResolvesSqliteJournalButOwnerFailureRetainsIt(bool business)
        {
            await using var db = await GamePlatform.Tests.Features.Teams.TeamsServiceTests.Database.Open();
            var owner = GamePlatform.Tests.Features.Teams.TeamsServiceTests.Owner();
            var player = GamePlatform.Tests.Features.Teams.TeamsServiceTests.Player;
            var operation = GamePlatform.Tests.Features.Teams.TeamsServiceTests.Operation;
            var details = business ? WireOptional<IReadOnlyDictionary<string, ErrorDetailValue>>.Present(new Dictionary<string, ErrorDetailValue> { ["operationId"] = ErrorDetailValue.FromString(operation.ToString("D")), ["receiptRetained"] = ErrorDetailValue.FromBoolean(true) }) : default;
            var wire = new ErrorResponse("friends-test", new CommonError(business ? "friends.target_unavailable" : "friends.owner_unavailable", business ? "validation" : "authorization", "hidden", false, default, details), 1790000000000);
            using var refresh = new AuthRefreshCoordinator();
            var provider = new FriendsHttpProvider(owner.Owner.AppId, owner.Owner.UserId, new BackendHttpConfiguration(new Uri("https://unit.invalid/"), owner.Owner.Backend), new Executor((_, __) => Reply(new MessagePackWireCodec().Encode(wire), business ? 400 : 403)), new Auth(), refresh);
            using var service = new FriendsService(owner, player, provider, new FriendsStateCodec(), db.Store, db.Current, () => 1790000000000);
            var command = new FriendsCommand(new OperationId(operation), "send", new FriendsCommandPayload(publicCode: "01ABCDEFGHJK"));
            await Assert.ThrowsAsync<FriendsOperationException>(() => service.ExecuteAsync(owner, command, CancellationToken.None));
            Assert.Equal(business, await service.ReadPendingAsync(owner, CancellationToken.None) == null);
        }
        private static V Diagnostic(string name)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(FriendsCodecTests.Root(), "contracts/friends/fixtures.json")));
            return FriendsCodecTests.Parse(doc.RootElement.GetProperty("fixtures").EnumerateArray().Single(x => x.GetProperty("name").GetString() == name).GetProperty("value"));
        }
        private static byte[] Fixture(string name, string schema) => new QualificationMessagePackCodec().Encode("friends.schema.json#/$defs/" + schema, Diagnostic(name));
        private static HttpResponseData Reply(byte[] bytes, int status = 200) => new HttpResponseData(status, new Dictionary<string, string> { ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType, ["X-Correlation-Id"] = "friends-test" }, bytes);
        private static ProductionAccountHttpProviders Providers(IHttpExecutor executor, IAuthSession auth, AuthRefreshCoordinator refresh) => new ProductionBackendHttpProviders(new BackendHttpConfiguration(new Uri("https://unit.invalid/"), new BackendNamespace("friends-test")), executor, auth, refresh).ForAccount(new AppId(Id), new PlatformUserId(Id), Id);
        private sealed class Executor : IHttpExecutor
        {
            private readonly Func<HttpRequestData, int, HttpResponseData> reply;
            public List<HttpRequestData> Requests = new List<HttpRequestData>();
            public Executor(Func<HttpRequestData, int, HttpResponseData> reply) => this.reply = reply;
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken token)
            {
                Requests.Add(request);
                return Task.FromResult(reply(request, Requests.Count));
            }
        }
        private sealed class Auth : IAuthSession
        {
            public int Refreshes;
            public string SessionKey => "friends-test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("old-token", 1));
            public Task<AccessTokenSnapshot> RefreshAsync(long generation, CancellationToken token)
            {
                Refreshes++;
                return Task.FromResult(new AccessTokenSnapshot("new-token", 2));
            }
        }
    }
}
