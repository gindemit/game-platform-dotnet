#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Tests.Transport
{
    /// <summary>Portable boundary tests with scripted ports; not live or production-composition evidence.</summary>
    public sealed class Cl010PrivateSyncHttpProviderTests : IDisposable
    {
        private readonly List<AuthRefreshCoordinator> ownedRefresh = new List<AuthRefreshCoordinator>();
        private static readonly AppId App = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-2000-7777-8888-999999999999"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-1000-7777-8888-999999999999");
        private static readonly Guid Account = Guid.Parse("0199f9a0-3000-7777-8888-999999999999");
        private static readonly Guid Epoch = Guid.Parse("0199f9a0-4000-7777-8888-999999999999");

        [Fact]
        public async Task BootstrapStartUsesFrozenGetQueryAndMapsAuthorizedBoundary()
        {
            var codec = Codec(StartResponse());
            var executor = new ScriptedExecutor(_ => Success());
            var result = await Provider(executor, codec).StartBootstrapAsync(Stream, CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(Stream, result.Value!.StreamId);
            Assert.Equal(7, result.Value.FinalizedThrough);
            Assert.Equal(8, result.Value.NextSequence);
            Assert.Equal(Token(1), result.Value.CopySession());
            var request = Assert.Single(executor.Requests);
            Assert.Equal("GET", request.Method);
            Assert.Equal("https://api.example.test/platform/v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/bootstrap?clientStreamId=0199f9a0-2000-7777-8888-999999999999", request.Uri);
            Assert.Equal(PrivateSyncHttpProvider.MessagePackMediaType, request.Headers["Accept"]);
            Assert.Equal("Bearer token-0", request.Headers["Authorization"]);
            Assert.False(request.Headers.ContainsKey("Content-Type"));
            Assert.Equal(0, request.BodyLength);
        }

        [Fact]
        public async Task BootstrapStartRejectsWrongAccountBeforeCoordinatorOrStorage()
        {
            var other = Guid.Parse("0199f9a0-3000-7777-8888-999999999998");
            var result = await Provider(new ScriptedExecutor(_ => Success()), Codec(StartResponse(other))).StartBootstrapAsync(Stream, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task BootstrapPageUsesFrozenPostAndPreservesFinalOrContinuationToken(bool hasMore)
        {
            var codec = Codec(new BootstrapPageResponse(
                TokenText(1), 9,
                new IProjectionSnapshotEntity[] { new ProjectionProfileSnapshot(new ProjectionProfileKey(), 2, Profile()) },
                hasMore, hasMore ? TokenText(2) : null, hasMore ? null : TokenText(3), 10));
            var executor = new ScriptedExecutor(_ => Success());
            var result = await Provider(executor, codec).GetBootstrapPageAsync(Token(1), Token(4), 65_536, CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(hasMore, result.Value!.HasMore);
            Assert.Equal(hasMore ? Token(2) : null, result.Value.CopyNextPageToken());
            Assert.Equal(hasMore ? null : Token(3), result.Value.CopyInitialPullCursor());
            var mutation = Assert.Single(result.Value.Entities);
            Assert.Equal("profile", mutation.Collection);
            Assert.Equal("self", mutation.EntityKey);
            Assert.Equal(ProjectionMutationKind.Upsert, mutation.Kind);
            Assert.Equal(new byte[] { 1, 2, 3 }, mutation.CopyPayload());
            var request = Assert.Single(executor.Requests);
            Assert.Equal("POST", request.Method);
            Assert.EndsWith("/v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/bootstrap/pages", request.Uri, StringComparison.Ordinal);
            Assert.Equal(PrivateSyncHttpProvider.MessagePackMediaType, request.Headers["Content-Type"]);
            var body = Assert.IsType<BootstrapPageRequest>(codec.Encoded.First());
            Assert.Equal(TokenText(1), body.SnapshotSession);
            Assert.Equal(TokenText(4), body.PageToken);
            Assert.Equal(65_536, body.MaxBytes);
        }

        [Fact]
        public async Task PullMapsCompleteGroupsRemovalsAndServerCursor()
        {
            var codec = Codec(new PullPage(new[]
            {
                new PullGroup(9, new IProjectionChange[]
                {
                    new ProjectionWalletUpsert(new ProjectionWalletKey("coins"), 3, new ProjectionWalletData(10)),
                    new ProjectionInventoryRemoval(new ProjectionInventoryKey("seed"), 4, "view_remove")
                })
            }, TokenText(8), true, 9, 10));
            var executor = new ScriptedExecutor(_ => Success());
            var result = await Provider(executor, codec).PullAsync(Token(7), 131_072, CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(9, result.Value!.CommittedThrough);
            Assert.Equal(Token(8), result.Value.CopyNextCursor());
            Assert.True(result.Value.HasMore);
            var changes = Assert.Single(result.Value.Groups).Changes;
            Assert.Equal(ProjectionMutationKind.Upsert, changes[0].Kind);
            Assert.Equal(ProjectionMutationKind.RemoveFromView, changes[1].Kind);
            Assert.Empty(changes[1].CopyPayload());
            var body = Assert.IsType<PullRequest>(codec.Encoded.First());
            Assert.Equal(TokenText(7), body.Cursor);
            Assert.Equal(131_072, body.MaxBytes);
        }

        [Fact]
        public async Task PullMapsFrozenInvalidationDistinctly()
        {
            var codec = Codec(new PullPage(new[]
            {
                new PullGroup(9, new IProjectionChange[]
                {
                    new ProjectionWalletRemoval(new ProjectionWalletKey("test.coin"), 4, "invalidation")
                })
            }, TokenText(8), false, 9, 10));

            var result = await Provider(new ScriptedExecutor(_ => Success()), codec).PullAsync(Token(7), 131_072, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var mutation = Assert.Single(Assert.Single(result.Value!.Groups).Changes);
            Assert.Equal(ProjectionMutationKind.Invalidation, mutation.Kind);
            Assert.Empty(mutation.CopyPayload());
        }

        [Fact]
        public async Task PullRejectsUnknownFrozenRemovalKind()
        {
            var codec = Codec(new PullPage(new[]
            {
                new PullGroup(9, new IProjectionChange[]
                {
                    new ProjectionWalletRemoval(new ProjectionWalletKey("test.coin"), 4, "future_kind")
                })
            }, TokenText(8), false, 9, 10));

            var result = await Provider(new ScriptedExecutor(_ => Success()), codec).PullAsync(Token(7), 131_072, CancellationToken.None);

            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
        }

        [Theory]
        [InlineData("history_expired")]
        [InlineData("visibility_changed")]
        [InlineData("log_epoch_changed")]
        [InlineData("cursor_invalidated")]
        public async Task PullReturnsFrozenResetWithoutInventingCheckpoint(string reason)
        {
            var codec = Codec((IPullResponse)new PullReset(Array.Empty<IProjectionChange>(), null, reason, 10));
            var result = await Provider(new ScriptedExecutor(_ => Success()), codec).PullAsync(Token(7), 262_144, CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.True(result.Value!.ResetRequired);
            Assert.Equal(reason, result.Value.ResetReason);
            Assert.Null(result.Value.CopyNextCursor());
            Assert.Empty(result.Value.Groups);
        }

        [Fact]
        public async Task DiagnosticJsonIsExplicitAndCodecFailureNeverFallsBack()
        {
            var codec = Codec(StartResponse());
            codec.ThrowOnDecode = true;
            var executor = new ScriptedExecutor(_ => Success(PrivateSyncHttpProvider.DiagnosticJsonMediaType));
            var result = await Provider(executor, codec, BackendWireRepresentation.DiagnosticJson).StartBootstrapAsync(Stream, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
            var request = Assert.Single(executor.Requests);
            Assert.Equal(PrivateSyncHttpProvider.DiagnosticJsonMediaType, request.Headers["Accept"]);
            Assert.Single(executor.Requests);
            Assert.Equal(1, codec.DecodeCount);
        }

        [Fact]
        public async Task RejectedAuthenticationRefreshesOnceAndReusesIdenticalBody()
        {
            var auth = new AuthSession();
            var codec = Codec((IPullResponse)new PullReset(Array.Empty<IProjectionChange>(), null, "cursor_invalidated", 10));
            var executor = new ScriptedExecutor(request => request.Headers["Authorization"] == "Bearer token-0" ? Response(401) : Success());
            var result = await Provider(executor, codec, auth: auth).PullAsync(Token(7), 131_072, CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(1, auth.RefreshCount);
            Assert.Equal(2, executor.Requests.Count);
            Assert.Equal(executor.Requests[0].CopyBody(), executor.Requests[1].CopyBody());
        }

        [Fact]
        public async Task SeparateProvidersShareOneAccountScopedRefreshFlight()
        {
            var auth = new AuthSession();
            using var refresh = new AuthRefreshCoordinator();
            var executor = new Racing401Executor();
            var first = Provider(executor, Codec(StartResponse()), auth: auth, refresh: refresh);
            var second = Provider(executor, Codec(StartResponse()), auth: auth, refresh: refresh);
            var results = await Task.WhenAll(
                first.StartBootstrapAsync(Stream, CancellationToken.None),
                second.StartBootstrapAsync(Stream, CancellationToken.None));
            Assert.All(results, result => Assert.True(result.IsSuccess));
            Assert.Equal(1, auth.RefreshCount);
            Assert.Equal(4, executor.Requests.Count);
            Assert.Equal(2, executor.Requests.Count(request => request.Headers["Authorization"] == "Bearer token-1"));
        }

        [Theory]
        [InlineData(HttpDeliveryCertainty.NotSent, RemoteFailureKind.NotSent)]
        [InlineData(HttpDeliveryCertainty.Uncertain, RemoteFailureKind.Dependency)]
        [InlineData(HttpDeliveryCertainty.ResponseReceived, RemoteFailureKind.Dependency)]
        public async Task ReadTransportFailuresDoNotClaimMutationUncertainty(HttpDeliveryCertainty certainty, RemoteFailureKind expected)
        {
            var result = await Provider(new ScriptedExecutor(_ => throw new HttpExecutionException(certainty, "safe")), Codec(StartResponse())).StartBootstrapAsync(Stream, CancellationToken.None);
            Assert.Equal(expected, result.Failure.Kind);
        }

        [Fact]
        public async Task CallerCancellationIsExplicitAndSendsNothing()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var executor = new ScriptedExecutor(_ => Success());
            var result = await Provider(executor, Codec(StartResponse())).StartBootstrapAsync(Stream, cancellation.Token);
            Assert.Equal(RemoteFailureKind.Cancelled, result.Failure.Kind);
            Assert.Empty(executor.Requests);
        }

        [Theory]
        [InlineData(405)]
        [InlineData(406)]
        [InlineData(413)]
        [InlineData(415)]
        public async Task PlainPreEnvelopeFailuresMapWithoutProtocolDecode(int status)
        {
            var codec = Codec(StartResponse());
            var result = await Provider(new ScriptedExecutor(_ => Response(status, "text/plain; charset=utf-8")), codec).StartBootstrapAsync(Stream, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Validation, result.Failure.Kind);
            Assert.Equal(0, codec.DecodeCount);
        }

        [Fact]
        public async Task ProtocolErrorEnvelopeMapsCategoryAndRetryAfter()
        {
            var error = new ErrorResponse("correlation", new CommonError("bootstrap_not_ready", "dependency", "not ready", true, WireOptional<int>.Present(12000), default), 10);
            var codec = Codec(error);
            var headers = Headers();
            headers["Retry-After"] = "12";
            var result = await Provider(new ScriptedExecutor(_ => new HttpResponseData(503, headers, new byte[] { 9 })), codec).StartBootstrapAsync(Stream, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Dependency, result.Failure.Kind);
            Assert.Equal(TimeSpan.FromSeconds(12), result.Failure.RetryAfter);
            Assert.Equal(1, codec.DecodeCount);
        }

        [Fact]
        public async Task ProtocolErrorRejectsHeaderEnvelopeCorrelationMismatch()
        {
            var error = new ErrorResponse("different-correlation", new CommonError("bootstrap_not_ready", "dependency", "not ready", true, default, default), 10);
            var result = await Provider(new ScriptedExecutor(_ => new HttpResponseData(503, Headers(), new byte[] { 9 })), Codec(error)).StartBootstrapAsync(Stream, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
        }

        [Fact]
        public async Task PageAndPullResponsesCannotExceedRequestedBudget()
        {
            var pageCodec = Codec(new BootstrapPageResponse(TokenText(1), 9, Array.Empty<IProjectionSnapshotEntity>(), false, null, TokenText(3), 10));
            var page = await Provider(new ScriptedExecutor(_ => new HttpResponseData(200, Headers(), new byte[65_537])), pageCodec)
                .GetBootstrapPageAsync(Token(1), Token(4), 65_536, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, page.Failure.Kind);
            Assert.Equal(0, pageCodec.DecodeCount);

            var pullCodec = Codec((IPullResponse)new PullReset(Array.Empty<IProjectionChange>(), null, "history_expired", 10));
            var pull = await Provider(new ScriptedExecutor(_ => new HttpResponseData(200, Headers(), new byte[131_073])), pullCodec)
                .PullAsync(Token(7), 131_072, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, pull.Failure.Kind);
            Assert.Equal(0, pullCodec.DecodeCount);
        }

        [Fact]
        public async Task OversizedPreEnvelopeFailsProtocolBeforeStatusMapping()
        {
            var codec = Codec(new BootstrapPageResponse(TokenText(1), 9, Array.Empty<IProjectionSnapshotEntity>(), false, null, TokenText(3), 10));
            var response = new HttpResponseData(503, new Dictionary<string, string> { ["Content-Type"] = "text/plain; charset=utf-8" }, new byte[65_537]);
            var result = await Provider(new ScriptedExecutor(_ => response), codec).GetBootstrapPageAsync(Token(1), Token(4), 65_536, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
            Assert.Equal(0, codec.DecodeCount);
        }

        [Fact]
        public async Task PullRejectsResetReasonOutsideFrozenSet()
        {
            var codec = Codec((IPullResponse)new PullReset(Array.Empty<IProjectionChange>(), null, "future_reason", 10));
            var result = await Provider(new ScriptedExecutor(_ => Success()), codec).PullAsync(Token(7), 131_072, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
        }

        [Fact]
        public async Task HostileResponsesFailClosedWithoutAdvancingSemanticState()
        {
            var malformed = new[]
            {
                Success("application/json"),
                new HttpResponseData(200, Headers(encoding: "gzip"), new byte[] { 9 }),
                new HttpResponseData(200, new Dictionary<string, string> { ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType }, new byte[] { 9 }),
                new HttpResponseData(200, Headers(), Array.Empty<byte>()),
                new HttpResponseData(200, Headers(), new byte[33])
            };
            foreach (var response in malformed)
            {
                var result = await Provider(new ScriptedExecutor(_ => response), Codec(StartResponse()), maximumResponseBytes: 32).StartBootstrapAsync(Stream, CancellationToken.None);
                Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
            }

            var wrongSession = Codec(new BootstrapPageResponse(TokenText(9), 9, Array.Empty<IProjectionSnapshotEntity>(), false, null, TokenText(3), 10));
            var page = await Provider(new ScriptedExecutor(_ => Success()), wrongSession).GetBootstrapPageAsync(Token(1), Token(4), 65_536, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, page.Failure.Kind);
        }

        [Fact]
        public async Task InvalidBudgetsAndTokensAreRejectedBeforeAuthOrSend()
        {
            var provider = Provider(new ScriptedExecutor(_ => Success()), Codec(StartResponse()));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.GetBootstrapPageAsync(new byte[11], Token(1), 65_536, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.GetBootstrapPageAsync(Token(1), Token(2), 65_535, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.PullAsync(Token(1), 131_071, CancellationToken.None));
        }

        public void Dispose()
        {
            foreach (var coordinator in ownedRefresh) coordinator.Dispose();
        }

        private PrivateSyncHttpProvider Provider(IHttpExecutor executor, StubCodec codec, BackendWireRepresentation representation = BackendWireRepresentation.MessagePack, AuthSession? auth = null, int maximumResponseBytes = 262_144, AuthRefreshCoordinator? refresh = null)
        {
            if (refresh == null) { refresh = new AuthRefreshCoordinator(); ownedRefresh.Add(refresh); }
            return new PrivateSyncHttpProvider(App, new PlatformUserId(Account), new BackendHttpConfiguration(new Uri("https://api.example.test/platform"), new BackendNamespace("test"), maximumResponseBytes), executor, codec, auth ?? new AuthSession(), refresh, representation);
        }

        private static BootstrapStartResponse StartResponse(Guid? account = null) => new BootstrapStartResponse(
            new CommonAccount(account ?? Account, 1, 1), new CommonMembership(App.Value, "active", 1, 2), TokenText(1), 9, 2, Epoch,
            new[] { "profile", "progression", "inventory", "wallet", "entitlements" }.Select(name => new BootstrapCollection(name)).ToArray(),
            TokenText(2), 100, 10, new CommonStreamState(Stream.Value, Installation, 7, 8, "active"));

        private static ProfileProfile Profile() => new ProfileProfile("Garden", default, default, 2, 10);
        private static byte[] Token(byte value) => Enumerable.Repeat(value, 12).Select(item => (byte)item).ToArray();
        private static string TokenText(byte value) => Convert.ToBase64String(Token(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static StubCodec Codec<T>(T decoded) where T : class => new StubCodec(decoded);
        private static HttpResponseData Success(string mediaType = PrivateSyncHttpProvider.MessagePackMediaType) => new HttpResponseData(200, Headers(mediaType), new byte[] { 9 });
        private static HttpResponseData Response(int status, string contentType = "text/plain; charset=utf-8") => new HttpResponseData(status, new Dictionary<string, string> { ["Content-Type"] = contentType }, new byte[] { 9 });
        private static Dictionary<string, string> Headers(string mediaType = PrivateSyncHttpProvider.MessagePackMediaType, string? encoding = null)
        {
            var headers = new Dictionary<string, string> { ["Content-Type"] = mediaType, ["X-Correlation-Id"] = "correlation" };
            if (encoding != null) headers["Content-Encoding"] = encoding;
            return headers;
        }

        private sealed class StubCodec : IWireCodec
        {
            private readonly object decoded;
            public StubCodec(object decoded) { this.decoded = decoded; }
            public bool ThrowOnDecode { get; set; }
            public int DecodeCount { get; private set; }
            public List<object> Encoded { get; } = new List<object>();
            public byte[] Encode<T>(T value)
            {
                Encoded.Add(value!);
                return new byte[] { 1, 2, 3 };
            }
            public T Decode<T>(byte[] payload)
            {
                DecodeCount++;
                if (ThrowOnDecode) throw new InvalidOperationException("malformed");
                return (T)decoded;
            }
        }

        private sealed class ScriptedExecutor : IHttpExecutor
        {
            private readonly Func<HttpRequestData, HttpResponseData> send;
            public ScriptedExecutor(Func<HttpRequestData, HttpResponseData> send) { this.send = send; }
            public List<HttpRequestData> Requests { get; } = new List<HttpRequestData>();
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                return Task.FromResult(send(request));
            }
        }

        private sealed class Racing401Executor : IHttpExecutor
        {
            private readonly TaskCompletionSource<bool> bothOldRequests = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int oldRequests;
            public ConcurrentBag<HttpRequestData> Requests { get; } = new ConcurrentBag<HttpRequestData>();
            public async Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                if (request.Headers["Authorization"] != "Bearer token-0") return Success();
                if (Interlocked.Increment(ref oldRequests) == 2) bothOldRequests.TrySetResult(true);
                await bothOldRequests.Task.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return Response(401);
            }
        }

        private sealed class AuthSession : IAuthSession
        {
            private long generation;
            private int refreshCount;
            public string SessionKey => "session";
            public int RefreshCount => Volatile.Read(ref refreshCount);
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = Volatile.Read(ref generation);
                return Task.FromResult(new AccessTokenSnapshot("token-" + value, value));
            }
            public Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref refreshCount);
                Interlocked.CompareExchange(ref generation, rejectedGeneration + 1, rejectedGeneration);
                return GetAsync(cancellationToken);
            }
        }
    }
}
