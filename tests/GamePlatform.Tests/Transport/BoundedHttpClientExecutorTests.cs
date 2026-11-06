using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Teams;
using GamePlatform.Features.Leaderboards;
using GamePlatform.Features.Profiles;
using GamePlatform.Features.Purchases;
using GamePlatform.Tests.Features.Daily;
using GamePlatform.Tests.Features.Leaderboards;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Tests.Transport
{
    public sealed class BoundedHttpClientExecutorTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        private static readonly PlatformUserId Account = new PlatformUserId(Guid.Parse("019952d1-0000-7000-8000-000000000004"));
        private static readonly Guid Installation = Guid.Parse("00112233-4455-4677-8899-aabbccddeeff");
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("019952d1-0000-7000-8000-000000000003"));

        [Fact]
        public async Task ExecutesFrozenRouteAndPreservesBoundedProtocolHeaders()
        {
            var handler = new DelegateHandler((request, _) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("Bearer token", request.Headers.Authorization!.ToString());
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
                };
                response.Content.Headers.TryAddWithoutValidation("Content-Type", ProvisioningHttpProvider.MediaType);
                response.Headers.TryAddWithoutValidation("X-Correlation-Id", "correlation-1");
                return Task.FromResult(response);
            });
            using var client = new HttpClient(handler);
            var executor = new BoundedHttpClientExecutor(client, Configuration());

            var response = await executor.SendAsync(Request("v1/apps/" + App + "/provision"), CancellationToken.None);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal(new byte[] { 1, 2, 3 }, response.CopyBody());
            Assert.Equal(ProvisioningHttpProvider.MediaType, response.Headers["Content-Type"]);
            Assert.Equal("correlation-1", response.Headers["X-Correlation-Id"]);
            Assert.Equal(1, handler.Calls);
        }

        [Theory]
        [InlineData("provision")]
        [InlineData("bootstrap/pages")]
        [InlineData("sync/pull")]
        [InlineData("sync/push")]
        [InlineData("teams/query")]
        [InlineData("teams/commands")]
        [InlineData("friends/query")]
        [InlineData("friends/commands")]
        [InlineData("leaderboards/query")]
        [InlineData("leaderboards/country")]
        [InlineData("leaderboards/friends/query")]
        [InlineData("sync/streams/inspect")]
        [InlineData("sync/streams/recover")]
        [InlineData("sync/receipts/lookup")]
        [InlineData("store/native/products")]
        [InlineData("store/native/intents")]
        [InlineData("store/native/proofs")]
        [InlineData("store/native/recover-proof")]
        [InlineData("store/native/status")]
        [InlineData("store/native/recovery")]
        [InlineData("store/offers")]
        [InlineData("daily/status")]
        [InlineData("profiles/public/query")]
        public async Task SupportedPostRoutesReachBoundedHttpExecutor(string route)
        {
            var handler = new DelegateHandler(async (request, _) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/platform/v1/apps/" + App + "/" + route, request.RequestUri!.AbsolutePath);
                Assert.Equal(string.Empty, request.RequestUri.Query);
                Assert.Equal("Bearer token", request.Headers.Authorization!.ToString());
                Assert.Equal(ProvisioningHttpProvider.MediaType, request.Headers.Accept.Single().MediaType);
                Assert.Equal(ProvisioningHttpProvider.MediaType, request.Content!.Headers.ContentType!.MediaType);
                Assert.Equal(new byte[] { 1 }, await request.Content.ReadAsByteArrayAsync());
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 2 }) };
            });
            using var client = new HttpClient(handler);
            var executor = new BoundedHttpClientExecutor(client, Configuration());
            var response = await executor.SendAsync(Request("v1/apps/" + App + "/" + route), CancellationToken.None);
            Assert.Equal(200, response.StatusCode);
            Assert.Equal(new byte[] { 2 }, response.CopyBody());
            Assert.Equal(1, handler.Calls);
        }

        public static IEnumerable<object[]> AddedRouteOperations()
        {
            foreach (var route in AddedRoutes())
                foreach (var retry in new[] { false, true })
                    yield return new object[] { route, retry };
        }

        private static IEnumerable<string> AddedRoutes() => new[]
        {
            "store/native/products", "store/native/intents", "store/native/proofs",
            "store/native/recover-proof", "store/native/status", "store/native/recovery",
            "store/offers", "daily/status", "profiles/public/query"
        };

        public static IEnumerable<object[]> NoncanonicalAddedRoutes()
        {
            var root = "v1/apps/" + App + "/";
            foreach (var route in AddedRoutes())
            {
                yield return new object[] { "GET", root + route };
                yield return new object[] { "PUT", root + route };
                yield return new object[] { "post", root + route };
                yield return new object[] { "POST", root + route + "?cursor=1" };
                yield return new object[] { "POST", root + route + "/" };
                yield return new object[] { "POST", root + route + "/extra" };
                var terminalStart = route.LastIndexOf('/') + 1;
                yield return new object[] { "POST", root + route.Substring(0, terminalStart) + route.Substring(terminalStart).ToUpperInvariant() };
                yield return new object[] { "POST", "v1/apps/invalid/" + route };
            }
            foreach (var route in new[] { "store/native", "store/native/unknown", "store/native/recover",
                "store/products", "store/commands", "daily/query", "daily/claim",
                "profiles/query", "profiles/public", "profiles/public/commands" })
                yield return new object[] { "POST", root + route };
        }

        [Theory]
        [MemberData(nameof(NoncanonicalAddedRoutes))]
        public async Task NoncanonicalAddedRoutesFailBeforeNetwork(string method, string path)
        {
            var handler = new DelegateHandler((_, _) => throw new InvalidOperationException("must not send"));
            using var client = new HttpClient(handler);
            var executor = new BoundedHttpClientExecutor(client, Configuration());
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token",
                ["Accept"] = ProvisioningHttpProvider.MediaType
            };
            if (method != "GET")
                headers["Content-Type"] = ProvisioningHttpProvider.MediaType;
            var request = new HttpRequestData(method, Configuration().Resolve(path).AbsoluteUri, headers,
                method == "GET" ? Array.Empty<byte>() : new byte[] { 1 });
            var error = await Assert.ThrowsAsync<HttpExecutionException>(() => executor.SendAsync(request, CancellationToken.None));
            Assert.Equal(HttpDeliveryCertainty.NotSent, error.Certainty);
            Assert.Equal(0, handler.Calls);
        }

        [Theory]
        [MemberData(nameof(AddedRouteOperations))]
        public async Task ActualProvidersUseExactAddedRoutesAndDecodeReplies(string route, bool retry)
        {
            var nativeCodec = new NativePurchaseMessagePackCodec();
            var id = Guid.Parse("0199f9a0-0000-7000-8000-000000000001");
            var intentKey = Guid.Parse("0199f9a0-0000-7000-8000-000000000002");
            var proof = new NativeProof("com.example.native", "synthetic.coins", "synthetic-token");
            var subject = Guid.Parse("0199f9a0-1000-7000-8000-000000000006");
            var profileQuery = new PublicProfileQuery(subject);
            byte[] expectedBody;
            byte[] responseBody;
            switch (route)
            {
                case "store/native/products":
                    expectedBody = nativeCodec.ProductsRequest();
                    responseBody = new byte[] { 0x81, 0xa8, 0x70, 0x72, 0x6f, 0x64, 0x75, 0x63, 0x74, 0x73, 0x90 };
                    break;
                case "store/native/intents":
                    expectedBody = nativeCodec.IntentRequest(intentKey, "synthetic.coins", 1);
                    responseBody = NativePendingResponse();
                    break;
                case "store/native/proofs":
                    expectedBody = nativeCodec.ProofRequest(id, proof);
                    responseBody = NativePendingResponse();
                    break;
                case "store/native/recover-proof":
                    expectedBody = nativeCodec.RecoverProofRequest(intentKey, proof);
                    responseBody = NativePendingResponse();
                    break;
                case "store/native/status":
                    expectedBody = nativeCodec.StatusRequest(id);
                    responseBody = NativePendingResponse();
                    break;
                case "store/native/recovery":
                    expectedBody = nativeCodec.RecoveryRequest(null, 10);
                    responseBody = new QualificationMessagePackCodec().Encode("native-purchases/schema.json#/$defs/recoveryResponse",
                        QualificationValue.Object(new[]
                        {
                            new KeyValuePair<string, QualificationValue>("intents", QualificationValue.Array(Array.Empty<QualificationValue>())),
                            new KeyValuePair<string, QualificationValue>("nextIntentId", QualificationValue.Null)
                        }));
                    break;
                case "store/offers":
                    expectedBody = new byte[] { 0x80 };
                    responseBody = new byte[] { 0x81, 0xa6, 0x6f, 0x66, 0x66, 0x65, 0x72, 0x73, 0x90 };
                    break;
                case "daily/status":
                    expectedBody = new byte[] { 0x80 };
                    responseBody = new DailyFeatureCodec().EncodeStatus(DailyServiceTests.Status());
                    break;
                case "profiles/public/query":
                    expectedBody = new PublicProfileMessagePackCodec().EncodeQuery(subject);
                    responseBody = new PublicProfileStateCodec().EncodeResponse(new PublicProfileResponse(App.Value, subject, 1_790_000_000_000, null));
                    break;
                default:
                    throw new InvalidOperationException("Unknown test route");
            }
            var sends = 0;
            var handler = new DelegateHandler(async (request, _) =>
            {
                sends++;
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/platform/v1/apps/" + App + "/" + route, request.RequestUri!.AbsolutePath);
                Assert.Equal(string.Empty, request.RequestUri.Query);
                Assert.Equal(retry && sends == 2 ? "Bearer fresh" : "Bearer token", request.Headers.Authorization!.ToString());
                Assert.Equal(ProvisioningHttpProvider.MediaType, request.Headers.Accept.Single().MediaType);
                Assert.Equal(ProvisioningHttpProvider.MediaType, request.Content!.Headers.ContentType!.MediaType);
                Assert.Equal(expectedBody, await request.Content.ReadAsByteArrayAsync());
                if (retry && sends == 1)
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new ByteArrayContent(Array.Empty<byte>()) };
                var reply = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(responseBody) };
                reply.Content.Headers.TryAddWithoutValidation("Content-Type", ProvisioningHttpProvider.MediaType);
                reply.Headers.TryAddWithoutValidation("X-Correlation-Id", "added-route-test");
                return reply;
            });
            using var client = new HttpClient(handler);
            using var refresh = new AuthRefreshCoordinator();
            var auth = new RefreshingAuth();
            var configuration = Configuration();
            var executor = new BoundedHttpClientExecutor(client, configuration);
            var native = new NativePurchaseHttpProvider(App, Account, configuration, executor, auth, refresh);
            NativeIntentState? intent = null;
            switch (route)
            {
                case "store/native/products":
                    Assert.Empty(await native.ProductsAsync(CancellationToken.None));
                    break;
                case "store/native/intents":
                    intent = await native.CreateIntentAsync(intentKey, "synthetic.coins", 1, CancellationToken.None);
                    break;
                case "store/native/proofs":
                    intent = await native.SubmitProofAsync(id, proof, CancellationToken.None);
                    break;
                case "store/native/recover-proof":
                    intent = await native.RecoverProofAsync(intentKey, proof, CancellationToken.None);
                    break;
                case "store/native/status":
                    intent = await native.StatusAsync(id, CancellationToken.None);
                    break;
                case "store/native/recovery":
                    var page = await native.RecoveryAsync(null, 10, CancellationToken.None);
                    Assert.Empty(page.Intents);
                    Assert.Null(page.NextIntentId);
                    break;
                case "store/offers":
                    var offers = await new StorefrontHttpProvider(App, Account, configuration, executor, auth, refresh).FetchAsync(CancellationToken.None);
                    Assert.True(offers.IsSuccess);
                    Assert.Empty(offers.Value!);
                    break;
                case "daily/status":
                    var daily = await new DailyHttpProvider(App, Account, configuration, executor, auth, refresh).StatusAsync(CancellationToken.None);
                    Assert.True(daily.IsSuccess);
                    Assert.Equal(DailyServiceTests.Status().UtcDate, daily.Value!.UtcDate);
                    break;
                case "profiles/public/query":
                    var profile = await new PublicProfileHttpProvider(App, Account, configuration, executor, auth, refresh).QueryAsync(profileQuery, CancellationToken.None);
                    Assert.True(profile.IsSuccess);
                    Assert.Equal(subject, profile.Value!.PlayerId);
                    Assert.Null(profile.Value.Profile);
                    break;
            }
            if (intent != null)
            {
                Assert.Equal("pending_verification", intent.State);
                Assert.Equal(id, intent.IntentId);
                Assert.Equal(intentKey, intent.IntentKey);
            }
            Assert.Equal(retry ? 2 : 1, handler.Calls);
            Assert.Equal(retry ? 1 : 0, auth.Refreshes);
        }

        private static byte[] NativePendingResponse()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "contracts", "native-purchases", "fixtures.json")))
                directory = directory.Parent;
            Assert.NotNull(directory);
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "contracts", "native-purchases", "fixtures.json")));
            return Convert.FromHexString(json.RootElement.GetProperty("pendingMessagePackHex").GetString()!);
        }

        [Fact]
        public async Task BootstrapGetRouteIsAdmittedWithItsExactQuery()
        {
            var path = "v1/apps/" + App + "/bootstrap?clientStreamId=" + Stream;
            var handler = new DelegateHandler((request, _) =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal(Configuration().Resolve(path), request.RequestUri);
                Assert.Null(request.Content);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) });
            });
            using var client = new HttpClient(handler);
            var result = await new BoundedHttpClientExecutor(client, Configuration()).SendAsync(GetRequest(path), CancellationToken.None);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal(1, handler.Calls);
        }

        [Theory]
        [InlineData("v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/unsupported")]
        [InlineData("v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/friends/gifts")]
        [InlineData("v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/leaderboards/friends/country")]
        [InlineData("v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/sync/streams/delete")]
        [InlineData("v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/sync/streams/recover/extra")]
        public async Task UnsupportedRoutesFailClosedBeforeNetwork(string path)
        {
            var handler = new DelegateHandler((_, _) => throw new InvalidOperationException("must not send"));
            using var client = new HttpClient(handler);
            var executor = new BoundedHttpClientExecutor(client, Configuration());

            var error = await Assert.ThrowsAsync<HttpExecutionException>(() => executor.SendAsync(Request(path), CancellationToken.None));

            Assert.Equal(HttpDeliveryCertainty.NotSent, error.Certainty);
            Assert.Equal(0, handler.Calls);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ProductionTeamsProviderUsesBoundedExecutorForCanonicalRoutes(bool mutation)
        {
            var operation = Guid.Parse("0199f9a0-1000-7000-8000-000000000005");
            var stateCodec = new TeamsStateCodec();
            var handler = new DelegateHandler(async (request, _) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/platform/v1/apps/" + App + "/teams/" + (mutation ? "commands" : "query"), request.RequestUri!.AbsolutePath);
                Assert.Equal("Bearer token", request.Headers.Authorization!.ToString());
                Assert.Equal(ProvisioningHttpProvider.MediaType, request.Content!.Headers.ContentType!.MediaType);
                var body = await request.Content.ReadAsByteArrayAsync();
                if (mutation)
                    Assert.Equal(operation, stateCodec.DecodeCommand(body).OperationId.Value);
                else
                    Assert.Equal(new TeamsMessagePackCodec().EncodeQuery(new GamePlatform.Wire.Contracts.Teams.TeamsQuery("mine")), body);
                var value = new TeamsResponse(App.Value, Account.Value, Installation, 1, mutation ? operation : null, 0, null, null,
                    Array.Empty<TeamSummary>(), Array.Empty<TeamMember>(), Array.Empty<TeamMessage>(), Array.Empty<TeamJoinRequest>(),
                    Array.Empty<TeamInvitation>(), Array.Empty<TeamHelpRequest>(), null, mutation ? "teams.leave.accepted" : null);
                var reply = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(stateCodec.EncodeResponse(value)) };
                reply.Content.Headers.TryAddWithoutValidation("Content-Type", ProvisioningHttpProvider.MediaType);
                reply.Headers.TryAddWithoutValidation("X-Correlation-Id", "teams-bounded-test");
                return reply;
            });
            using var client = new HttpClient(handler);
            using var refresh = new AuthRefreshCoordinator();
            var configuration = Configuration();
            var provider = new ProductionBackendHttpProviders(configuration, new BoundedHttpClientExecutor(client, configuration), new StaticAuth(), refresh)
                .ForAccount(App, Account, Installation).Teams;
            var result = mutation
                ? await provider.ExecuteAsync(new TeamsCommand(new OperationId(operation), "leave", Installation), CancellationToken.None)
                : await provider.QueryAsync(new TeamsQuery("mine"), CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(Account.Value, result.Value!.AccountId);
            Assert.Equal(1, handler.Calls);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ProductionLeaderboardsProviderUsesBoundedExecutorForCanonicalRoutes(bool mutation)
        {
            var command = LeaderboardsServiceTests.Command();
            var codec = new LeaderboardsMessagePackCodec();
            var handler = new DelegateHandler(async (request, _) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/platform/v1/apps/" + App + "/leaderboards/" + (mutation ? "country" : "query"), request.RequestUri!.AbsolutePath);
                Assert.Equal(string.Empty, request.RequestUri.Query);
                Assert.Equal("Bearer token", request.Headers.Authorization!.ToString());
                Assert.Equal(ProvisioningHttpProvider.MediaType, request.Headers.Accept.Single().MediaType);
                Assert.Equal(ProvisioningHttpProvider.MediaType, request.Content!.Headers.ContentType!.MediaType);
                var body = await request.Content.ReadAsByteArrayAsync();
                Assert.Equal(mutation ? new LeaderboardsStateCodec().EncodeCountry(command) :
                    codec.EncodeQuery(new GamePlatform.Wire.Contracts.Leaderboards.LeaderboardsQuery("players-monthly", "global", null, null, 30)), body);
                var country = LeaderboardsServiceTests.CountryResponse(command);
                var bytes = mutation ? codec.EncodeCountryResponse(new GamePlatform.Wire.Contracts.Leaderboards.LeaderboardsCountryResponse(
                    country.OperationId, country.BoardId, country.ParticipantKind, country.ParticipantId, country.CountryCode, country.EffectiveAt)) :
                    new LeaderboardsStateCodec().EncodeResponse(LeaderboardsServiceTests.Response());
                var reply = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
                reply.Content.Headers.TryAddWithoutValidation("Content-Type", ProvisioningHttpProvider.MediaType);
                reply.Headers.TryAddWithoutValidation("X-Correlation-Id", "leaderboards-bounded-test");
                return reply;
            });
            using var client = new HttpClient(handler);
            using var refresh = new AuthRefreshCoordinator();
            var configuration = Configuration();
            var provider = new ProductionBackendHttpProviders(configuration, new BoundedHttpClientExecutor(client, configuration), new StaticAuth(), refresh)
                .ForAccount(App, Account, Installation).Leaderboards;
            if (mutation)
            {
                var result = await provider.SetCountryAsync(command, CancellationToken.None);
                Assert.True(result.IsSuccess);
                Assert.Equal(command.OperationId.Value, result.Value!.OperationId);
                Assert.Equal("DE", result.Value.CountryCode);
            }
            else
            {
                var result = await provider.QueryAsync(new LeaderboardsQuery("players-monthly"), CancellationToken.None);
                Assert.True(result.IsSuccess);
                Assert.Equal("players-monthly", result.Value!.BoardId);
                Assert.Equal(9_007_199_254_740_993, result.Value.Entries.Single().Score);
            }
            Assert.Equal(1, handler.Calls);
        }

        [Theory]
        [InlineData("GET", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/leaderboards/query")]
        [InlineData("GET", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/leaderboards/country")]
        [InlineData("POST", "v1/apps/invalid/leaderboards/query")]
        [InlineData("POST", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/leaderboards/query?cursor=1")]
        [InlineData("POST", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/leaderboards/country?country=DE")]
        [InlineData("POST", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/leaderboards/query/extra")]
        [InlineData("POST", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/leaderboards/country/extra")]
        [InlineData("POST", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/leaderboards/commands")]
        [InlineData("POST", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/leaderboards/delete")]
        [InlineData("POST", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/leaderboards/Query")]
        [InlineData("GET", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/teams/query")]
        [InlineData("GET", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/teams/commands")]
        [InlineData("POST", "v1/apps/invalid/teams/query")]
        [InlineData("POST", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/teams/query?cursor=1")]
        [InlineData("POST", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/teams/query/extra")]
        [InlineData("POST", "v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/teams/delete")]
        public async Task NoncanonicalSocialRoutesFailBeforeNetwork(string method, string path)
        {
            var handler = new DelegateHandler((_, _) => throw new InvalidOperationException("must not send"));
            using var client = new HttpClient(handler);
            var executor = new BoundedHttpClientExecutor(client, Configuration());
            var headers = new Dictionary<string, string> { ["Authorization"] = "Bearer token", ["Accept"] = ProvisioningHttpProvider.MediaType };
            if (method == "POST")
                headers["Content-Type"] = ProvisioningHttpProvider.MediaType;
            var request = new HttpRequestData(method, Configuration().Resolve(path).AbsoluteUri, headers, method == "POST" ? new byte[] { 1 } : Array.Empty<byte>());
            var error = await Assert.ThrowsAsync<HttpExecutionException>(() => executor.SendAsync(request, CancellationToken.None));
            Assert.Equal(HttpDeliveryCertainty.NotSent, error.Certainty);
            Assert.Equal(0, handler.Calls);
        }

        [Fact]
        public async Task PushRouteIsAdmittedWithTheFrozenMessagePackProfile()
        {
            var handler = new DelegateHandler((request, _) =>
            {
                Assert.Equal("/platform/v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/sync/push", request.RequestUri!.AbsolutePath);
                Assert.Equal(ProvisioningHttpProvider.MediaType, request.Content!.Headers.ContentType!.MediaType);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 1 })
                });
            });
            using var client = new HttpClient(handler);
            var executor = new BoundedHttpClientExecutor(client, Configuration());

            var response = await executor.SendAsync(Request("v1/apps/" + App + "/sync/push"), CancellationToken.None);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal(1, handler.Calls);
        }

        [Fact]
        public async Task RewardReceiptGetRouteIsAdmittedWithoutABody()
        {
            var operation = Guid.Parse("0199f9a0-0700-7000-8000-000000000010");
            var handler = new DelegateHandler((request, _) => { Assert.Equal(HttpMethod.Get, request.Method); Assert.Null(request.Content); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1 }) }); });
            using var client = new HttpClient(handler);
            var executor = new BoundedHttpClientExecutor(client, Configuration());
            var response = await executor.SendAsync(GetRequest("v1/apps/" + App + "/gameplay/reward-receipts/" + operation), CancellationToken.None);
            Assert.Equal(200, response.StatusCode);
            Assert.Equal(1, handler.Calls);
        }

        [Fact]
        public async Task OversizeResponseFailsAfterResponseWithoutAllocatingPastBound()
        {
            var handler = new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Enumerable.Repeat((byte)7, 65).ToArray())
            }));
            using var client = new HttpClient(handler);
            var executor = new BoundedHttpClientExecutor(client, Configuration(64));

            var error = await Assert.ThrowsAsync<HttpExecutionException>(() => executor.SendAsync(Request("v1/apps/" + App + "/provision"), CancellationToken.None));

            Assert.Equal(HttpDeliveryCertainty.ResponseReceived, error.Certainty);
            Assert.Equal(1, handler.Calls);
        }

        [Fact]
        public async Task TransportFailureAfterDispatchIsUncertainAndCallerCancellationIsPreserved()
        {
            var failedHandler = new DelegateHandler((_, _) => throw new HttpRequestException("test failure"));
            using var failedClient = new HttpClient(failedHandler);
            var failedExecutor = new BoundedHttpClientExecutor(failedClient, Configuration());
            var error = await Assert.ThrowsAsync<HttpExecutionException>(() => failedExecutor.SendAsync(Request("v1/apps/" + App + "/provision"), CancellationToken.None));
            Assert.Equal(HttpDeliveryCertainty.Uncertain, error.Certainty);

            var admitted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellingHandler = new DelegateHandler(async (_, cancellationToken) =>
            {
                admitted.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException();
            });
            using var cancellingClient = new HttpClient(cancellingHandler);
            var cancellingExecutor = new BoundedHttpClientExecutor(cancellingClient, Configuration());
            using var cancellation = new CancellationTokenSource();
            var pending = cancellingExecutor.SendAsync(Request("v1/apps/" + App + "/provision"), cancellation.Token);
            await admitted.Task;
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }

        [Fact]
        public async Task ProductionCompositionUsesMessagePackAndFrozenProvidersEndToEnd()
        {
            var codec = new MessagePackWireCodec();
            var handler = new DelegateHandler(async (request, _) =>
            {
                var requestBytes = await request.Content!.ReadAsByteArrayAsync();
                var provision = codec.Decode<ProvisionRequest>(requestBytes);
                Assert.Equal(Installation, provision.InstallationId);
                Assert.Equal(Stream.Value, provision.ClientStreamId);
                Assert.DoesNotContain("json", request.Headers.Accept.Single().MediaType!, StringComparison.OrdinalIgnoreCase);

                var responseValue = new ProvisionResponse(
                    new CommonAccount(Account.Value, 1, 1),
                    new CommonMembership(App.Value, "active", 1, 1),
                    Stream.Value, 1, 1);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(codec.Encode(responseValue))
                };
                response.Content.Headers.TryAddWithoutValidation("Content-Type", ProvisioningHttpProvider.MediaType);
                response.Headers.TryAddWithoutValidation("X-Correlation-Id", "composition-test");
                return response;
            });
            using var client = new HttpClient(handler);
            var configuration = Configuration();
            var executor = new BoundedHttpClientExecutor(client, configuration);
            using var refresh = new AuthRefreshCoordinator();
            var providers = new ProductionBackendHttpProviders(configuration, executor, new StaticAuth(), refresh);

            var result = await providers.Provisioning.ProvisionAsync(App, Installation, Stream, CancellationToken.None);
            var accountProviders = providers.ForAccount(App, Account, Installation);

            Assert.True(result.IsSuccess);
            Assert.Equal(Account, result.Value!.AccountId);
            Assert.IsType<PrivateSyncHttpProvider>(accountProviders.PrivateSync);
            Assert.IsType<CommandPushHttpProvider>(accountProviders.CommandPush);
            Assert.IsType<CommandReceiptHttpProvider>(accountProviders.CommandReceipts);
            Assert.IsType<CommandStreamRecoveryHttpProvider>(accountProviders.CommandStreamRecovery);
            Assert.IsType<G3RewardReceiptHttpProvider>(accountProviders.RewardReceipts);
            Assert.Equal(1, handler.Calls);
        }

        private static BackendHttpConfiguration Configuration(int maximumResponseBytes = 262_144) =>
            new BackendHttpConfiguration(new Uri("https://api.example.test/platform"), new BackendNamespace("test"), maximumResponseBytes);

        private static HttpRequestData Request(string path) => new HttpRequestData(
            "POST",
            Configuration().Resolve(path).AbsoluteUri,
            new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token",
                ["Accept"] = ProvisioningHttpProvider.MediaType,
                ["Content-Type"] = ProvisioningHttpProvider.MediaType
            },
            new byte[] { 1 });

        private static HttpRequestData GetRequest(string path) => new HttpRequestData("GET", Configuration().Resolve(path).AbsoluteUri, new Dictionary<string, string> { { "Authorization", "Bearer token" }, { "Accept", ProvisioningHttpProvider.MediaType } }, Array.Empty<byte>());

        private sealed class DelegateHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;
            public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
            {
                this.send = send;
            }
            public int Calls { get; private set; }
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                var response = await send(request, cancellationToken);
                response.RequestMessage ??= request;
                return response;
            }
        }

        private sealed class StaticAuth : IAuthSession
        {
            public string SessionKey => "production-composition-test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new AccessTokenSnapshot("token", 0));
            public Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken) =>
                Task.FromResult(new AccessTokenSnapshot("token", 0));
        }

        private sealed class RefreshingAuth : IAuthSession
        {
            public int Refreshes { get; private set; }
            public string SessionKey => "added-route-test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new AccessTokenSnapshot("token", 1));
            public Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken)
            {
                Assert.Equal(1, rejectedGeneration);
                Refreshes++;
                return Task.FromResult(new AccessTokenSnapshot("fresh", 2));
            }
        }
    }
}
