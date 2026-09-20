#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
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
        [InlineData("v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/unsupported")]
        public async Task UnsupportedRoutesFailClosedBeforeNetwork(string path)
        {
            var handler = new DelegateHandler((_, _) => throw new InvalidOperationException("must not send"));
            using var client = new HttpClient(handler);
            var executor = new BoundedHttpClientExecutor(client, Configuration());

            var error = await Assert.ThrowsAsync<HttpExecutionException>(() => executor.SendAsync(Request(path), CancellationToken.None));

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

        private sealed class DelegateHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;
            public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) { this.send = send; }
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
    }
}
