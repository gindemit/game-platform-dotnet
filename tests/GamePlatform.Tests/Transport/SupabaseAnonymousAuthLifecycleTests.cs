#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Accounts;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http.Supabase;

namespace GamePlatform.Tests.Transport
{
    public sealed class SupabaseAnonymousAuthLifecycleTests
    {
        private static readonly Guid Subject = Guid.Parse("019a1234-1111-7777-8888-999999999999");
        private static readonly Guid OtherSubject = Guid.Parse("019a1234-2222-7777-8888-999999999999");

        [Fact]
        public async Task SignupPersistsOnlyRefreshRecordAndPublishesConfiguredPrincipal()
        {
            var store = new Store(); var executor = new Scripted(Response(SessionJson(Subject, "access-one", "refresh-one", 2_000)));
            var result = await Provider(executor, store).AuthenticateAsync(CancellationToken.None);
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, result.Kind); Assert.Equal(Subject.ToString("D"), result.Principal!.Subject); Assert.Equal("https://auth.example.test/auth/v1", result.Principal.Issuer);
            Assert.Equal("access-one", (await result.Session!.GetAsync(CancellationToken.None)).Value);
            Assert.Single(executor.Requests); Assert.EndsWith("/auth/v1/signup", executor.Requests[0].Uri, StringComparison.Ordinal); Assert.Equal("{\"data\":{}}", Encoding.UTF8.GetString(executor.Requests[0].CopyBody()));
            Assert.DoesNotContain("access-one", Encoding.UTF8.GetString(store.Value!)); Assert.Contains("refresh-one", Encoding.UTF8.GetString(store.Value!));
        }

        [Fact]
        public async Task MalformedSignupResponsePreservesPendingMarkerAndRequiresRecovery()
        {
            var store = new Store(); var result = await Provider(new Scripted(Response("{not-json")), store).AuthenticateAsync(CancellationToken.None);
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, result.Kind); Assert.NotNull(store.Value); Assert.Equal(0, store.Value![5]);
        }

        [Fact]
        public async Task ExpiredAccessRefreshesOnceAndRotatesSecureRefreshRecordBeforePublication()
        {
            var store = new Store(); var executor = new Scripted(Response(SessionJson(Subject, "access-old", "refresh-old", 1)), Response(SessionJson(Subject, "access-new", "refresh-new", 3_000)));
            var result = await Provider(executor, store).AuthenticateAsync(CancellationToken.None);
            var session = result.Session!; var first = session.GetAsync(CancellationToken.None); var second = session.RefreshAsync(0, CancellationToken.None);
            var tokens = await Task.WhenAll(first, second);
            Assert.All(tokens, value => Assert.Equal("access-new", value.Value)); Assert.All(tokens, value => Assert.Equal(1, value.Generation)); Assert.Equal(2, executor.Requests.Count);
            Assert.EndsWith("/auth/v1/token?grant_type=refresh_token", executor.Requests[1].Uri, StringComparison.Ordinal); Assert.DoesNotContain("access-new", Encoding.UTF8.GetString(store.Value!)); Assert.Contains("refresh-new", Encoding.UTF8.GetString(store.Value!));
        }

        [Fact]
        public async Task RestartUsesRefreshOnlyAndRetainsStablePrincipal()
        {
            var store = new Store(); var first = await Provider(new Scripted(Response(SessionJson(Subject, "access-one", "refresh-one", 2_000))), store).AuthenticateAsync(CancellationToken.None);
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, first.Kind);
            var executor = new Scripted(Response(SessionJson(Subject, "access-two", "refresh-two", 3_000)));
            var reopened = await Provider(executor, store).AuthenticateAsync(CancellationToken.None);
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, reopened.Kind); Assert.Equal(Subject.ToString("D"), reopened.Principal!.Subject); Assert.Single(executor.Requests); Assert.EndsWith("/auth/v1/token?grant_type=refresh_token", executor.Requests[0].Uri, StringComparison.Ordinal);
        }

        [Fact]
        public async Task InvalidRefreshAndSubjectMismatchRequireRecoveryWithoutNewSignup()
        {
            var store = new Store(); await Provider(new Scripted(Response(SessionJson(Subject, "access-one", "refresh-one", 2_000))), store).AuthenticateAsync(CancellationToken.None); var before = (byte[])store.Value!.Clone();
            var invalid = new Scripted(Response(SessionJson(OtherSubject, "access-two", "refresh-two", 3_000)));
            var result = await Provider(invalid, store).AuthenticateAsync(CancellationToken.None);
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, result.Kind); Assert.Single(invalid.Requests); Assert.Equal(before, store.Value);
        }

        [Fact]
        public async Task SignupUncertaintyAndZeroSignupRecoveryNeverCreateAnotherAnonymousUser()
        {
            var store = new Store(); var executor = new Scripted(new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "test")); var provider = Provider(executor, store);
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await provider.AuthenticateAsync(CancellationToken.None)).Kind);
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await provider.AuthenticateAsync(CancellationToken.None)).Kind); Assert.Single(executor.Requests); Assert.NotNull(store.Value); Assert.Equal(0, store.Value![5]);
        }

        [Fact]
        public async Task NotSentSignupClearsPendingAndAControlledLaterAttemptMaySignup()
        {
            var store = new Store(); var executor = new Scripted(new HttpExecutionException(HttpDeliveryCertainty.NotSent, "test"), Response(SessionJson(Subject, "access", "refresh", 2_000))); var provider = Provider(executor, store);
            Assert.Equal(AccountAuthLifecycleKind.UnavailableOffline, (await provider.AuthenticateAsync(CancellationToken.None)).Kind); Assert.Null(store.Value);
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, (await provider.AuthenticateAsync(CancellationToken.None)).Kind); Assert.Equal(2, executor.Requests.Count);
        }

        [Fact]
        public async Task KnownRefreshNetworkFailureIsOfflineAndSecureStoreFailuresAreRecovery()
        {
            var store = new Store(); await Provider(new Scripted(Response(SessionJson(Subject, "access", "refresh", 2_000))), store).AuthenticateAsync(CancellationToken.None);
            var offline = await Provider(new Scripted(new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "test")), store).AuthenticateAsync(CancellationToken.None);
            Assert.Equal(AccountAuthLifecycleKind.UnavailableOffline, offline.Kind); Assert.Equal(Subject.ToString("D"), offline.Principal!.Subject);
            var failedStore = new Store { FailWrites = true }; Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await Provider(new Scripted(), failedStore).AuthenticateAsync(CancellationToken.None)).Kind);
        }

        [Fact]
        public async Task CallerCancellationReturnsCancelledAndDoesNotPublishSession()
        {
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            var result = await Provider(new Scripted(), new Store()).AuthenticateAsync(cancellation.Token);
            Assert.Equal(AccountAuthLifecycleKind.Cancelled, result.Kind); Assert.Null(result.Session);
        }

        [Fact]
        public async Task ConcurrentAuthenticateFencesSecondSignupBehindFirstPublication()
        {
            var gate = new TaskCompletionSource<HttpResponseData>(TaskCreationOptions.RunContinuationsAsynchronously); var executor = new Scripted(gate.Task, Response(SessionJson(Subject, "access-two", "refresh-two", 3_000))); var provider = Provider(executor, new Store());
            var first = provider.AuthenticateAsync(CancellationToken.None); await executor.FirstSend.Task; var second = provider.AuthenticateAsync(CancellationToken.None); gate.SetResult(Response(SessionJson(Subject, "access-one", "refresh-one", 2_000)));
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, (await first).Kind); Assert.Equal(AccountAuthLifecycleKind.Authenticated, (await second).Kind); Assert.Equal(2, executor.Requests.Count); Assert.EndsWith("/signup", executor.Requests[0].Uri, StringComparison.Ordinal); Assert.EndsWith("/token?grant_type=refresh_token", executor.Requests[1].Uri, StringComparison.Ordinal);
        }

        [Fact]
        public async Task BoundedExecutorRejectsCrossAuthorityInvalidRouteRedirectAndOversizedBody()
        {
            var configuration = Configuration(maximumResponseBytes: 1_024); var handler = new DelegateHandler((request, _) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/signup", StringComparison.Ordinal)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect) { RequestMessage = request, Headers = { Location = new Uri("https://other.example.test/auth/v1/signup") } });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1_025]), RequestMessage = request });
            });
            using var client = new HttpClient(handler); var executor = new BoundedSupabaseAuthHttpExecutor(client, configuration); var headers = Headers();
            await Assert.ThrowsAsync<HttpExecutionException>(() => executor.SendAsync(new HttpRequestData("POST", "https://other.example.test/auth/v1/signup", headers, Encoding.UTF8.GetBytes("{}")), CancellationToken.None));
            await Assert.ThrowsAsync<HttpExecutionException>(() => executor.SendAsync(new HttpRequestData("POST", configuration.Resolve("unsupported").AbsoluteUri, headers, Encoding.UTF8.GetBytes("{}")), CancellationToken.None));
            await Assert.ThrowsAsync<HttpExecutionException>(() => executor.SendAsync(new HttpRequestData("POST", configuration.Resolve("signup").AbsoluteUri, headers, Encoding.UTF8.GetBytes("{}")), CancellationToken.None));
            await Assert.ThrowsAsync<HttpExecutionException>(() => executor.SendAsync(new HttpRequestData("POST", configuration.Resolve("token?grant_type=refresh_token").AbsoluteUri, headers, Encoding.UTF8.GetBytes("{}")), CancellationToken.None));
        }

        private static SupabaseAnonymousAuthLifecycle Provider(IHttpExecutor executor, Store store) => new SupabaseAnonymousAuthLifecycle(Configuration(), executor, store, new Clock());
        private static SupabaseAuthConfiguration Configuration(int maximumResponseBytes = 65_536) => new SupabaseAuthConfiguration(new Uri("https://auth.example.test/auth/v1/"), "https://auth.example.test/auth/v1", new BackendNamespace("test-backend"), "public-test-key", "platform.supabase.session", maximumResponseBytes);
        private static IReadOnlyDictionary<string, string> Headers() => new Dictionary<string, string> { ["apikey"] = "public-test-key", ["Accept"] = "application/json", ["Content-Type"] = "application/json" };
        private static HttpResponseData Response(string json) => new HttpResponseData(200, new Dictionary<string, string> { ["Content-Type"] = "application/json; charset=utf-8" }, Encoding.UTF8.GetBytes(json));
        private static string SessionJson(Guid subject, string access, string refresh, long expiresAt) => "{\"access_token\":\"" + access + "\",\"refresh_token\":\"" + refresh + "\",\"expires_at\":" + expiresAt + ",\"user\":{\"id\":\"" + subject.ToString("D") + "\"}}";

        private sealed class Clock : IUnixMillisecondClock { public long GetUnixMilliseconds() => 1_000_000; }
        private sealed class Store : ISupabaseAnonymousSessionStore
        {
            public bool IsAvailable { get; set; } = true; public bool FailWrites { get; set; } public byte[]? Value { get; private set; }
            public Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult(Value == null ? null : (byte[])Value.Clone());
            public Task WriteAsync(string key, byte[] secret, CancellationToken cancellationToken) { if (FailWrites) throw new InvalidOperationException("test"); Value = (byte[])secret.Clone(); return Task.CompletedTask; }
            public Task DeleteAsync(string key, CancellationToken cancellationToken) { Value = null; return Task.CompletedTask; }
        }
        private sealed class Scripted : IHttpExecutor
        {
            private readonly Queue<object> values = new Queue<object>(); public List<HttpRequestData> Requests { get; } = new List<HttpRequestData>(); public TaskCompletionSource<bool> FirstSend { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Scripted(params object[] values) { foreach (var value in values) this.values.Enqueue(value); }
            public async Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
            {
                Requests.Add(request); FirstSend.TrySetResult(true); if (values.Count == 0) throw new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "test"); var value = values.Dequeue();
                if (value is Exception error) throw error; if (value is Task<HttpResponseData> task) return await task.ConfigureAwait(false); return (HttpResponseData)value;
            }
        }
        private sealed class DelegateHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send; public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) { this.send = send; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
        }
    }
}
