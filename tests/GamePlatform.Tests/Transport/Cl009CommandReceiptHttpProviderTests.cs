#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>Scripted receipt boundary tests; not live backend or production-composition evidence.</summary>
    public sealed class Cl009CommandReceiptHttpProviderTests : IDisposable
    {
        private static readonly AppId App = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        private static readonly PlatformUserId Account = new PlatformUserId(Guid.Parse("0199f9a0-3000-7777-8888-999999999999"));
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-2000-7777-8888-999999999999"));
        private static readonly OperationId Operation = new OperationId(Guid.Parse("019952d1-0000-7000-8000-000000000003"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-1000-7777-8888-999999999999");
        private readonly List<AuthRefreshCoordinator> ownedRefresh = new List<AuthRefreshCoordinator>();

        [Fact]
        public async Task FoundAcceptedReceiptUsesFrozenRouteAndPreservesTerminalBytes()
        {
            var terminal = new PushAccepted(Operation.Value, 155, 922, new PushGameplayCompletionRecordedResult());
            var codec = new StubCodec(new RecoveryReceiptResponse(Stream.Value, true, terminal, 10, Operation.Value, 155));
            var executor = new ScriptedExecutor(_ => Success());

            var result = await Provider(executor, codec).LookupAsync(Command(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value!.Found);
            Assert.Equal(RemoteCommandStatus.Accepted, result.Value.Outcome!.Status);
            Assert.Equal(new byte[] { 1, 2, 3 }, result.Value.Outcome.Result);
            Assert.Equal(155, result.Value.ObservedFinalizedThrough);
            var request = Assert.Single(executor.Requests);
            Assert.Equal("POST", request.Method);
            Assert.Equal("https://api.example.test/platform/v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/sync/receipts/lookup", request.Uri);
            Assert.Equal(PrivateSyncHttpProvider.MessagePackMediaType, request.Headers["Accept"]);
            Assert.Equal("Bearer token-0", request.Headers["Authorization"]);
            var body = Assert.IsType<RecoveryReceiptRequest>(codec.Encoded[0]);
            Assert.Equal(Stream.Value, body.ClientStreamId);
            Assert.Equal(Operation.Value, body.OperationId);
            Assert.Equal(Installation, body.InstallationId);
            Assert.Equal(155, body.Sequence);
            Assert.Equal(new string('0', 64), body.Fingerprint);
            Assert.Same(terminal, codec.Encoded[1]);
        }

        [Fact]
        public async Task FoundTerminalRejectionMapsWithoutTurningItIntoRetryableFailure()
        {
            var error = new CommonError("command.invalid", "validation", "rejected", false, default, default);
            var terminal = new PushTerminalRejected(Operation.Value, 155, error);
            var result = await Provider(new ScriptedExecutor(_ => Success()),
                new StubCodec(new RecoveryReceiptResponse(Stream.Value, true, terminal, 10, Operation.Value, 160)))
                .LookupAsync(Command(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value!.Found);
            Assert.Equal(RemoteCommandStatus.TerminalRejected, result.Value.Outcome!.Status);
        }

        [Fact]
        public async Task QualificationCodecRoundTripMapsFrozenReceiptWithoutProductionRegistration()
        {
            var candidate = new DesktopQualificationWireCodec();
            var codec = new TestOnlyQualificationPortAdapter(candidate);
            var terminal = new PushAccepted(Operation.Value, 155, 922, new PushGameplayCompletionRecordedResult());
            var body = candidate.Encode(new RecoveryReceiptResponse(Stream.Value, true, terminal, 10, Operation.Value, 155));
            var response = new HttpResponseData(200, Headers(), body);

            var result = await Provider(new ScriptedExecutor(_ => response), codec).LookupAsync(Command(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(RemoteCommandStatus.Accepted, result.Value!.Outcome!.Status);
            Assert.NotEmpty(result.Value.Outcome.Result!);
            Assert.IsType<PushAccepted>(candidate.Decode<PushAccepted>(result.Value.Outcome.Result!));
        }

        [Fact]
        public async Task AuthorizedAbsenceRemainsAnObservationAndDoesNotInventOutcome()
        {
            var result = await Provider(new ScriptedExecutor(_ => Success()),
                new StubCodec(new RecoveryReceiptResponse(Stream.Value, false, null, 10, Operation.Value, 154)))
                .LookupAsync(Command(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(result.Value!.Found);
            Assert.Null(result.Value.Outcome);
            Assert.Equal(154, result.Value.ObservedFinalizedThrough);
        }

        [Fact]
        public async Task MissingReceiptAtOrBelowWatermarkFailsClosed()
        {
            var result = await Provider(new ScriptedExecutor(_ => Success()),
                new StubCodec(new RecoveryReceiptResponse(Stream.Value, false, null, 10, Operation.Value, 155)))
                .LookupAsync(Command(), CancellationToken.None);

            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task ReceiptIdentityAndTerminalIdentityMustMatch(bool wrongEnvelopeOperation, bool wrongTerminalSequence)
        {
            var envelopeOperation = wrongEnvelopeOperation ? Guid.Parse("019952d1-0000-7000-8000-000000000004") : Operation.Value;
            var terminalSequence = wrongTerminalSequence ? 154 : 155;
            var terminal = new PushAccepted(Operation.Value, terminalSequence, 922, new PushGameplayCompletionRecordedResult());
            var response = new RecoveryReceiptResponse(Stream.Value, true, terminal, 10, envelopeOperation, 155);

            var result = await Provider(new ScriptedExecutor(_ => Success()), new StubCodec(response)).LookupAsync(Command(), CancellationToken.None);

            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
        }

        [Fact]
        public async Task AuthenticationRefreshesOnceAndReusesIdenticalLookupBody()
        {
            var auth = new AuthSession();
            var response = new RecoveryReceiptResponse(Stream.Value, false, null, 10, Operation.Value, 154);
            var executor = new ScriptedExecutor(request => request.Headers["Authorization"] == "Bearer token-0" ? Plain(401) : Success());

            var result = await Provider(executor, new StubCodec(response), auth: auth).LookupAsync(Command(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, auth.RefreshCount);
            Assert.Equal(2, executor.Requests.Count);
            Assert.Equal(executor.Requests[0].CopyBody(), executor.Requests[1].CopyBody());
        }

        [Theory]
        [InlineData(HttpDeliveryCertainty.NotSent, RemoteFailureKind.NotSent)]
        [InlineData(HttpDeliveryCertainty.Uncertain, RemoteFailureKind.Dependency)]
        [InlineData(HttpDeliveryCertainty.ResponseReceived, RemoteFailureKind.Dependency)]
        public async Task ReadFailureNeverClaimsACommandOutcome(HttpDeliveryCertainty certainty, RemoteFailureKind expected)
        {
            var result = await Provider(
                new ScriptedExecutor(_ => throw new HttpExecutionException(certainty, "lookup failed")),
                new StubCodec(new object())).LookupAsync(Command(), CancellationToken.None);

            Assert.Equal(expected, result.Failure.Kind);
        }

        [Fact]
        public async Task ErrorEnvelopeIsBoundToCorrelationAndMapsRetryAfter()
        {
            var error = new ErrorResponse("correlation", new CommonError("receipt.unavailable", "dependency", "later", true, default, default), 10);
            var headers = Headers();
            headers["Retry-After"] = "3";
            var result = await Provider(new ScriptedExecutor(_ => new HttpResponseData(503, headers, new byte[] { 9 })), new StubCodec(error))
                .LookupAsync(Command(), CancellationToken.None);

            Assert.Equal(RemoteFailureKind.Dependency, result.Failure.Kind);
            Assert.Equal(TimeSpan.FromSeconds(3), result.Failure.RetryAfter);
        }

        [Fact]
        public async Task DiagnosticModeIsExplicitAndDecodeFailureNeverFallsBack()
        {
            var codec = new StubCodec(new object()) { ThrowOnDecode = true };
            var executor = new ScriptedExecutor(_ => Success(PrivateSyncHttpProvider.DiagnosticJsonMediaType));
            var result = await Provider(executor, codec, BackendWireRepresentation.DiagnosticJson).LookupAsync(Command(), CancellationToken.None);

            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
            Assert.Equal(PrivateSyncHttpProvider.DiagnosticJsonMediaType, Assert.Single(executor.Requests).Headers["Accept"]);
            Assert.Equal(1, codec.DecodeCount);
        }

        [Fact]
        public async Task UnsupportedFingerprintIsRejectedBeforeAuthenticationOrSend()
        {
            var executor = new ScriptedExecutor(_ => Success());
            var auth = new AuthSession();
            var command = new RemoteCommand(Operation, Stream, 155, "gameplay.session.completed", 1, 2, new byte[] { 8 }, new byte[31]);

            var result = await Provider(executor, new StubCodec(new object()), auth: auth).LookupAsync(command, CancellationToken.None);

            Assert.Equal(RemoteFailureKind.Protocol, result.Failure.Kind);
            Assert.Empty(executor.Requests);
            Assert.Equal(0, auth.GetCount);
        }

        public void Dispose()
        {
            foreach (var coordinator in ownedRefresh) coordinator.Dispose();
        }

        private CommandReceiptHttpProvider Provider(IHttpExecutor executor, IWireCodec codec,
            BackendWireRepresentation representation = BackendWireRepresentation.MessagePack,
            AuthSession? auth = null, AuthRefreshCoordinator? refresh = null)
        {
            if (refresh == null) { refresh = new AuthRefreshCoordinator(); ownedRefresh.Add(refresh); }
            return new CommandReceiptHttpProvider(App, Account, Installation,
                new BackendHttpConfiguration(new Uri("https://api.example.test/platform"), new BackendNamespace("test")),
                executor, codec, auth ?? new AuthSession(), refresh, representation);
        }

        private static RemoteCommand Command() => new RemoteCommand(Operation, Stream, 155, "gameplay.session.completed", 1, 1, new byte[] { 8 }, new byte[32]);
        private static HttpResponseData Success(string mediaType = PrivateSyncHttpProvider.MessagePackMediaType) => new HttpResponseData(200, Headers(mediaType), new byte[] { 9 });
        private static HttpResponseData Plain(int status) => new HttpResponseData(status, new Dictionary<string, string> { ["Content-Type"] = "text/plain" }, new byte[] { 9 });
        private static Dictionary<string, string> Headers(string mediaType = PrivateSyncHttpProvider.MessagePackMediaType) =>
            new Dictionary<string, string> { ["Content-Type"] = mediaType, ["X-Correlation-Id"] = "correlation" };

        private sealed class StubCodec : IWireCodec
        {
            private readonly object decoded;
            public StubCodec(object decoded) { this.decoded = decoded; }
            public bool ThrowOnDecode { get; set; }
            public int DecodeCount { get; private set; }
            public List<object> Encoded { get; } = new List<object>();
            public byte[] Encode<T>(T value) { Encoded.Add(value!); return new byte[] { 1, 2, 3 }; }
            public T Decode<T>(byte[] payload)
            {
                DecodeCount++;
                if (ThrowOnDecode) throw new InvalidOperationException("malformed");
                return (T)decoded;
            }
        }

        private sealed class TestOnlyQualificationPortAdapter : IWireCodec
        {
            private readonly DesktopQualificationWireCodec candidate;
            public TestOnlyQualificationPortAdapter(DesktopQualificationWireCodec candidate) { this.candidate = candidate; }
            public byte[] Encode<T>(T value)
            {
                return value switch
                {
                    RecoveryReceiptRequest item => candidate.Encode(item),
                    PushAccepted item => candidate.Encode(item),
                    PushTerminalRejected item => candidate.Encode(item),
                    _ => throw new QualificationCodecException("Unsupported test bridge DTO.")
                };
            }
            public T Decode<T>(byte[] payload)
            {
                if (typeof(T) == typeof(RecoveryReceiptResponse)) return (T)(object)candidate.Decode<RecoveryReceiptResponse>(payload);
                if (typeof(T) == typeof(ErrorResponse)) return (T)(object)candidate.Decode<ErrorResponse>(payload);
                throw new QualificationCodecException("Unsupported test bridge DTO.");
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

        private sealed class AuthSession : IAuthSession
        {
            private long generation;
            private int refreshCount;
            private int getCount;
            public string SessionKey => "session";
            public int RefreshCount => Volatile.Read(ref refreshCount);
            public int GetCount => Volatile.Read(ref getCount);
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref getCount);
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
