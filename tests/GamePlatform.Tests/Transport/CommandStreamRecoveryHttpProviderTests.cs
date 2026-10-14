#nullable enable
using System;
using System.Collections.Generic;
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
    public sealed class CommandStreamRecoveryHttpProviderTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        private static readonly PlatformUserId Account = new PlatformUserId(Guid.Parse("019952d1-0000-7000-8000-000000000004"));
        private static readonly Guid Installation = Guid.Parse("00112233-4455-4677-8899-aabbccddeeff");
        private static readonly ClientStreamId Prior = new ClientStreamId(Guid.Parse("019952d1-0000-7000-8000-000000000003"));
        private static readonly ClientStreamId Successor = new ClientStreamId(Guid.Parse("019952d1-0000-7000-8000-000000000005"));
        private static readonly Guid RecoveryId = Guid.Parse("019952d1-0000-7000-8000-000000000006");
        private const string Token = "c3ludGhldGljLWluc3BlY3Rpb24";

        [Fact]
        public async Task InspectsAccountBoundStreamThroughQualifiedMessagePack()
        {
            var codec = new MessagePackWireCodec();
            var response = new RecoveryInspectResponse(new CommonStreamState(Prior.Value, Installation, 154, 155, "active"),
                Token, 1_789_555_501_000, 1_789_555_201_000);
            var executor = new ScriptedExecutor(request =>
            {
                Assert.EndsWith("/sync/streams/inspect", request.Uri);
                var body = codec.Decode<RecoveryInspectRequest>(request.CopyBody());
                Assert.Equal(Prior.Value, body.ClientStreamId);
                Assert.Equal(Installation, body.InstallationId);
                return Success(codec.Encode(response));
            });
            using var refresh = new AuthRefreshCoordinator();
            var result = await Provider(executor, refresh).InspectAsync(Prior, CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(154, result.Value!.FinalizedThrough);
            Assert.Equal(155, result.Value.NextSequence);
            Assert.Equal(CommandStreamState.Active, result.Value.State);
            Assert.Equal(Token, result.Value.InspectionToken);
            Assert.Single(executor.Requests);
        }

        [Fact]
        public async Task RecoveryReusesImmutableBodyAfterLostResponseAndExpiredToken()
        {
            var codec = new MessagePackWireCodec();
            var response = new RecoveryStreamResponse(Prior.Value, Successor.Value, 1, 154, 1_789_555_901_000, RecoveryId);
            // A commit may have happened before a response was lost. The caller retries the same request.
            var calls = 0;
            var executor = new ScriptedExecutor(request =>
            {
                calls++;
                var body = codec.Decode<RecoveryStreamRequest>(request.CopyBody());
                Assert.Equal(Installation, body.InstallationId);
                Assert.Equal("backup_restored", body.Reason);
                Assert.Equal(Token, body.InspectionToken);
                Assert.Equal(154, body.ExpectedFinalizedThrough);
                Assert.Equal(RecoveryId, body.RecoveryId);
                if (calls == 1)
                    throw new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "response lost");
                return Success(codec.Encode(response));
            });
            using var refresh = new AuthRefreshCoordinator();
            var provider = Provider(executor, refresh);
            var recovery = Request();
            var first = await provider.RecoverAsync(recovery, CancellationToken.None);
            var second = await provider.RecoverAsync(recovery, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.OutcomeUncertain, first.Failure.Kind);
            Assert.True(second.IsSuccess);
            Assert.Equal(1, second.Value!.NextSequence);
            Assert.Equal(executor.Requests[0].CopyBody(), executor.Requests[1].CopyBody());
        }

        [Fact]
        public async Task MismatchedSuccessIsProtocolFailure()
        {
            var codec = new MessagePackWireCodec();
            var wrong = new RecoveryStreamResponse(Prior.Value, Successor.Value, 1, 154, 1_789_555_201_000,
                Guid.Parse("019952d1-0000-7000-8000-000000000007"));
            using var refresh = new AuthRefreshCoordinator();
            var result = await Provider(new ScriptedExecutor(_ => Success(codec.Encode(wrong))), refresh)
                .RecoverAsync(Request(), CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
        }

        [Theory]
        [InlineData(HttpDeliveryCertainty.NotSent, RemoteFailureKind.NotSent)]
        [InlineData(HttpDeliveryCertainty.Uncertain, RemoteFailureKind.OutcomeUncertain)]
        [InlineData(HttpDeliveryCertainty.ResponseReceived, RemoteFailureKind.OutcomeUncertain)]
        public async Task MutationFailurePreservesDeliveryCertainty(HttpDeliveryCertainty certainty, RemoteFailureKind expected)
        {
            using var refresh = new AuthRefreshCoordinator();
            var result = await Provider(new ScriptedExecutor(_ => throw new HttpExecutionException(certainty, "failed")), refresh)
                .RecoverAsync(Request(), CancellationToken.None);
            Assert.Equal(expected, result.Failure.Kind);
        }

        [Fact]
        public async Task StructuredApplicationErrorMapsCategoryAndPlainErrorMapsStatus()
        {
            var codec = new MessagePackWireCodec();
            var error = new ErrorResponse("correlation", new CommonError("stream_changed", "conflict", "changed", false, default, default),
                1_789_555_201_000);
            using var refresh = new AuthRefreshCoordinator();
            var structured = await Provider(new ScriptedExecutor(_ => new HttpResponseData(409, Headers(), codec.Encode(error))), refresh)
                .RecoverAsync(Request(), CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Conflict, structured.Failure.Kind);
            var plain = await Provider(new ScriptedExecutor(_ => new HttpResponseData(503,
                new Dictionary<string, string> { ["Content-Type"] = "text/plain" }, new byte[] { 1 })), refresh)
                .RecoverAsync(Request(), CancellationToken.None);
            Assert.Equal(RemoteFailureKind.Dependency, plain.Failure.Kind);
        }

        [Theory]
        [InlineData(409, "conflict", "inspection_expired", "correlation", RemoteFailureKind.InspectionExpired)]
        [InlineData(400, "conflict", "inspection_expired", "correlation", RemoteFailureKind.Conflict)]
        [InlineData(409, "validation", "inspection_expired", "correlation", RemoteFailureKind.Validation)]
        [InlineData(409, "conflict", "stream_changed", "correlation", RemoteFailureKind.Conflict)]
        [InlineData(409, "conflict", "inspection_expired", "other", RemoteFailureKind.Protocol)]
        public async Task OnlyValidatedRecoveryExpiryProvesUncommittedRequest(int status, string category, string code, string correlation, RemoteFailureKind expected)
        {
            var codec = new MessagePackWireCodec();
            var error = new ErrorResponse(correlation, new CommonError(code, category, "rejected", false, default, default), 100);
            using var refresh = new AuthRefreshCoordinator();
            var provider = Provider(new ScriptedExecutor(_ => new HttpResponseData(status, Headers(), codec.Encode(error))), refresh);
            Assert.Equal(expected, (await provider.RecoverAsync(Request(), CancellationToken.None)).Failure.Kind);
            Assert.NotEqual(RemoteFailureKind.InspectionExpired, (await provider.InspectAsync(Prior, CancellationToken.None)).Failure.Kind);
        }

        private static CommandStreamRecoveryRequest Request() => new CommandStreamRecoveryRequest(Prior, Successor,
            RecoveryId, CommandStreamRecoveryReason.BackupRestored, Token, 154);

        private static CommandStreamRecoveryHttpProvider Provider(IHttpExecutor executor, AuthRefreshCoordinator refresh) =>
            new CommandStreamRecoveryHttpProvider(App, Account, Installation,
                new BackendHttpConfiguration(new Uri("https://api.example.test/platform"), new BackendNamespace("test")),
                executor, new MessagePackWireCodec(), new StaticAuth(), refresh);

        private static HttpResponseData Success(byte[] body) => new HttpResponseData(200, Headers(), body);
        private static Dictionary<string, string> Headers() => new Dictionary<string, string>
        {
            ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType,
            ["X-Correlation-Id"] = "correlation"
        };

        private sealed class ScriptedExecutor : IHttpExecutor
        {
            public ScriptedExecutor(Func<HttpRequestData, HttpResponseData> send)
            {
                Send = send;
            }
            public Func<HttpRequestData, HttpResponseData> Send { get; set; }
            public List<HttpRequestData> Requests { get; } = new List<HttpRequestData>();
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(Send(request));
            }
        }

        private sealed class StaticAuth : IAuthSession
        {
            public string SessionKey => "recovery-test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new AccessTokenSnapshot("token", 0));
            public Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken) =>
                Task.FromResult(new AccessTokenSnapshot("token", 0));
        }
    }
}
