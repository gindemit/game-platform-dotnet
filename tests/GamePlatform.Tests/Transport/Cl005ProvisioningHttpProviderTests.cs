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
    public sealed class Cl005ProvisioningHttpProviderTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-1000-7777-8888-999999999999");
        private static readonly Guid Stream = Guid.Parse("0199f9a0-2000-7777-8888-999999999999");

        [Fact]
        public async Task BuildsBoundedAuthenticatedMessagePackRequestAndValidatesOwnerResponse()
        {
            var executor = new ScriptedExecutor(_ => SuccessResponse());
            var provider = Provider(executor);
            var result = await Call(provider, CancellationToken.None);
            Assert.True(result.IsSuccess);
            var sent = Assert.Single(executor.Requests);
            Assert.Equal("POST", sent.Method);
            Assert.Equal("https://api.example.test/platform/v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/provision", sent.Uri);
            Assert.Equal(ProvisioningHttpProvider.MediaType, sent.Headers["Accept"]);
            Assert.Equal(ProvisioningHttpProvider.MediaType, sent.Headers["Content-Type"]);
            Assert.Equal("Bearer token-0", sent.Headers["Authorization"]);
            Assert.Equal(new byte[] { 1, 2, 3 }, sent.CopyBody());
        }

        [Theory]
        [InlineData(HttpDeliveryCertainty.NotSent, RemoteFailureKind.NotSent)]
        [InlineData(HttpDeliveryCertainty.Uncertain, RemoteFailureKind.OutcomeUncertain)]
        public async Task MutationTimeoutIsClassifiedAndNeverAutomaticallyReplayed(HttpDeliveryCertainty certainty, RemoteFailureKind expected)
        {
            var executor = new ScriptedExecutor(_ => throw new HttpExecutionException(certainty, "safe diagnostic"));
            var result = await Call(Provider(executor), CancellationToken.None);
            Assert.False(result.IsSuccess);
            Assert.Equal(expected, result.Failure.Kind);
            Assert.Single(executor.Requests);
        }

        [Fact]
        public async Task Concurrent401ResponsesCoordinateOneRefreshPerSession()
        {
            var auth = new RefreshingAuth();
            var executor = new Racing401Executor();
            var provider = Provider(executor, auth: auth);
            var results = await Task.WhenAll(
                Call(provider, CancellationToken.None),
                Call(provider, CancellationToken.None));
            Assert.All(results, result => Assert.True(result.IsSuccess));
            Assert.Equal(1, auth.RefreshCount);
            Assert.Equal(4, executor.Requests.Count);
            Assert.Equal(2, executor.Requests.Count(request => request.Headers["Authorization"] == "Bearer token-1"));
        }

        [Theory]
        [InlineData(403, RemoteFailureKind.Authorization)]
        [InlineData(409, RemoteFailureKind.Conflict)]
        [InlineData(500, RemoteFailureKind.Server)]
        [InlineData(503, RemoteFailureKind.Dependency)]
        public async Task HttpFailuresMapWithoutDecodingOrRetry(int status, RemoteFailureKind expected)
        {
            var codec = new StubCodec();
            var executor = new ScriptedExecutor(_ => Response(status));
            var result = await Call(Provider(executor, codec), CancellationToken.None);
            Assert.False(result.IsSuccess);
            Assert.Equal(expected, result.Failure.Kind);
            Assert.Equal(0, codec.DecodeCount);
            Assert.Single(executor.Requests);
        }

        [Fact]
        public async Task RateLimitCarriesBoundedRetryAfter()
        {
            var response = Response(429, new Dictionary<string, string> { ["Retry-After"] = "12" });
            var result = await Call(Provider(new ScriptedExecutor(_ => response)), CancellationToken.None);
            Assert.Equal(RemoteFailureKind.RateLimited, result.Failure.Kind);
            Assert.Equal(TimeSpan.FromSeconds(12), result.Failure.RetryAfter);
        }

        [Fact]
        public async Task WrongMediaCompressionTruncationOversizeAndWrongOwnerFailProtocol()
        {
            var cases = new[]
            {
                Response(200, SuccessHeaders("application/json"), new byte[] { 9 }),
                Response(200, SuccessHeaders(ProvisioningHttpProvider.MediaType, "gzip"), new byte[] { 9 }),
                Response(200, SuccessHeaders(), Array.Empty<byte>()),
                Response(200, SuccessHeaders(), new byte[33]),
            };
            foreach (var response in cases)
            {
                var result = await Call(Provider(new ScriptedExecutor(_ => response), maximumResponseBytes: 32), CancellationToken.None);
                Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
            }
            var wrongOwnerCodec = new StubCodec
            {
                DecodedResponse = new ProvisionResponse(new CommonAccount(Guid.Parse("0199f9a0-3000-7777-8888-999999999999"), 1, 1), new CommonMembership(Guid.Parse("0199f9a0-4000-7777-8888-999999999999"), "active", 1, 1), Stream, 1, 1)
            };
            var wrongOwner = await Call(Provider(new ScriptedExecutor(_ => SuccessResponse()), wrongOwnerCodec), CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, wrongOwner.Failure.Kind);
            var brokenCodec = new StubCodec { ThrowOnDecode = true };
            var truncated = await Call(Provider(new ScriptedExecutor(_ => SuccessResponse()), brokenCodec), CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, truncated.Failure.Kind);
        }

        [Fact]
        public async Task CallerCancellationIsExplicitAndNoRequestIsSent()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var executor = new ScriptedExecutor(_ => SuccessResponse());
            var result = await Call(Provider(executor), cancellation.Token);
            Assert.Equal(RemoteFailureKind.Cancelled, result.Failure.Kind);
            Assert.Empty(executor.Requests);
        }

        [Fact]
        public void ConfigurationRejectsAuthorityConfusionAndRequestBuffersAreDefensive()
        {
            Assert.Throws<ArgumentException>(() => new BackendHttpConfiguration(new Uri("http://api.example.test"), new BackendNamespace("test")));
            Assert.Throws<ArgumentException>(() => new BackendHttpConfiguration(new Uri("https://user@api.example.test"), new BackendNamespace("test")));
            var config = new BackendHttpConfiguration(new Uri("https://api.example.test/root"), new BackendNamespace("test"));
            Assert.Throws<ArgumentException>(() => config.Resolve("../escape"));
            var body = new byte[] { 1 };
            var request = new HttpRequestData("POST", "https://api.example.test/v1", new Dictionary<string, string>(), body);
            body[0] = 9;
            Assert.Equal(1, request.CopyBody()[0]);
        }

        private static ProvisioningHttpProvider Provider(IHttpExecutor executor, StubCodec? codec = null, RefreshingAuth? auth = null, int maximumResponseBytes = 262_144) =>
            new ProvisioningHttpProvider(new BackendHttpConfiguration(new Uri("https://api.example.test/platform"), new BackendNamespace("test"), maximumResponseBytes), executor, codec ?? new StubCodec(), auth ?? new RefreshingAuth());

        private static Task<RemoteResult<ProvisioningSnapshot>> Call(ProvisioningHttpProvider provider, CancellationToken cancellationToken) =>
            provider.ProvisionAsync(App, Installation, new ClientStreamId(Stream), cancellationToken);

        private static ProvisionResponse Value() => new ProvisionResponse(
            new CommonAccount(Guid.Parse("0199f9a0-3000-7777-8888-999999999999"), 1, 1),
            new CommonMembership(App.Value, "active", 1, 1), Stream, 1, 1);

        private static HttpResponseData SuccessResponse() => Response(200, SuccessHeaders(), new byte[] { 9 });
        private static HttpResponseData Response(int status, IReadOnlyDictionary<string, string>? headers = null, byte[]? body = null) =>
            new HttpResponseData(status, headers ?? new Dictionary<string, string>(), body ?? new byte[] { 9 });
        private static IReadOnlyDictionary<string, string> SuccessHeaders(string contentType = ProvisioningHttpProvider.MediaType, string? encoding = null)
        {
            var headers = new Dictionary<string, string> { ["Content-Type"] = contentType, ["X-Correlation-Id"] = "test-correlation" };
            if (encoding != null) headers["Content-Encoding"] = encoding;
            return headers;
        }

        private sealed class ScriptedExecutor : IHttpExecutor
        {
            private readonly Func<HttpRequestData, HttpResponseData> send;
            public ScriptedExecutor(Func<HttpRequestData, HttpResponseData> send) { this.send = send; }
            public ConcurrentBag<HttpRequestData> Requests { get; } = new ConcurrentBag<HttpRequestData>();
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                return Task.FromResult(send(request));
            }
        }

        private sealed class StubCodec : IWireCodec
        {
            public bool ThrowOnDecode { get; set; }
            public int DecodeCount { get; private set; }
            public ProvisionResponse? DecodedResponse { get; set; }
            public byte[] Encode<T>(T value) => new byte[] { 1, 2, 3 };
            public T Decode<T>(byte[] payload)
            {
                DecodeCount++;
                if (ThrowOnDecode) throw new InvalidOperationException("truncated");
                return (T)(object)(DecodedResponse ?? Value());
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
                if (request.Headers["Authorization"] != "Bearer token-0") return SuccessResponse();
                if (Interlocked.Increment(ref oldRequests) == 2) bothOldRequests.TrySetResult(true);
                await bothOldRequests.Task.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return Response(401);
            }
        }

        private sealed class RefreshingAuth : IAuthSession
        {
            private long generation;
            private int refreshCount;
            public string SessionKey => "session-1";
            public int RefreshCount => Volatile.Read(ref refreshCount);
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = Volatile.Read(ref generation);
                return Task.FromResult(new AccessTokenSnapshot("token-" + current, current));
            }
            public Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref refreshCount);
                var current = Interlocked.CompareExchange(ref generation, rejectedGeneration + 1, rejectedGeneration);
                var effective = current == rejectedGeneration ? rejectedGeneration + 1 : current;
                return Task.FromResult(new AccessTokenSnapshot("token-" + effective, effective));
            }
        }
    }
}
