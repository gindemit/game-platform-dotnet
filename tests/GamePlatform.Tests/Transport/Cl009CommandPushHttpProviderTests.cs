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
    /// <summary>Scripted transport boundaries over the production typed MessagePack codec; not live backend evidence.</summary>
    public sealed class Cl009CommandPushHttpProviderTests : IDisposable
    {
        private static readonly AppId App = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        private static readonly PlatformUserId Account = new PlatformUserId(Guid.Parse("0199f9a0-3000-7777-8888-999999999999"));
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-2000-7777-8888-999999999999"));
        private static readonly OperationId Operation = new OperationId(Guid.Parse("019952d1-0000-7000-8000-000000000003"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-1000-7777-8888-999999999999");
        private readonly MessagePackWireCodec codec = new MessagePackWireCodec();
        private readonly List<AuthRefreshCoordinator> ownedRefresh = new List<AuthRefreshCoordinator>();

        [Fact]
        public async Task ProfilePatchUsesOneOperationFrozenRequestAndMapsAcceptedTerminalBytes()
        {
            var command = ProfileCommand();
            var accepted = new PushAccepted(Operation.Value, 155, 922, new PushProfileUpdatedResult(12));
            var executor = new ScriptedExecutor(_ => Response(new PushResponse(Stream.Value, new IPushResult[] { accepted }, 155, 1_789_555_201_000)));

            var result = await Provider(executor).SendAsync(command, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(RemoteCommandStatus.Accepted, result.Value!.Status);
            var terminal = codec.Decode<PushAccepted>(result.Value.Result!);
            Assert.Equal(12, Assert.IsType<PushProfileUpdatedResult>(terminal.Result).ProfileRevision);
            var request = Assert.Single(executor.Requests);
            Assert.Equal("POST", request.Method);
            Assert.Equal("https://api.example.test/platform/v1/apps/01890f3e-7a6b-7c8d-9e0f-102030405060/sync/push", request.Uri);
            Assert.Equal(PrivateSyncHttpProvider.MessagePackMediaType, request.Headers["Accept"]);
            Assert.Equal(PrivateSyncHttpProvider.MessagePackMediaType, request.Headers["Content-Type"]);
            Assert.Equal("Bearer token-0", request.Headers["Authorization"]);
            var wire = codec.Decode<PushRequest>(request.CopyBody());
            Assert.Equal(Stream.Value, wire.ClientStreamId);
            var operation = Assert.Single(wire.Operations);
            Assert.Equal(Operation.Value, operation.OperationId);
            Assert.Equal(Installation, operation.InstallationId);
            Assert.Equal(155, operation.Sequence);
            Assert.Equal("profile.patch", operation.Type);
            Assert.Equal(1_789_555_200_000L, operation.ClientCreatedAt);
            var patch = Assert.IsType<ProfilePatchCommand>(operation.Payload);
            Assert.Equal(5, patch.ExpectedRevision);
            Assert.True(patch.DisplayName.IsPresent);
            Assert.Equal("Ari", patch.DisplayName.Value);
        }

        [Fact]
        public async Task GameplayBodyUsesTypedMessagePackAndTerminalRejectionIsFinal()
        {
            var rejected = new PushTerminalRejected(Operation.Value, 155,
                Error("gameplay.invalid", "validation", false));
            var executor = new ScriptedExecutor(_ => Response(new PushResponse(Stream.Value, new IPushResult[] { rejected }, 155, 1_789_555_201_000)));

            var result = await Provider(executor).SendAsync(GameplayCommand(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(RemoteCommandStatus.TerminalRejected, result.Value!.Status);
            Assert.IsType<PushTerminalRejected>(codec.Decode<PushTerminalRejected>(result.Value.Result!));
            var request = codec.Decode<PushRequest>(Assert.Single(executor.Requests).CopyBody());
            var payload = Assert.IsType<GameplayCompletionCommand>(Assert.Single(request.Operations).Payload);
            Assert.Equal("session-155", payload.Session);
            Assert.Equal(12_500, payload.DurationTicks);
            Assert.Equal(-1, payload.Metrics["bonus"]);
        }

        [Fact]
        public async Task RetryWaitingAndUpgradeRemainNonterminalWithTheirFrozenSemantics()
        {
            var retry = new PushRetryable(Operation.Value, 155,
                new CommonError("dependency.unavailable", "dependency", "Retry", true, WireOptional<int>.Present(1_000), default));
            var retryResult = await Provider(new ScriptedExecutor(_ => Response(new PushResponse(Stream.Value, new IPushResult[] { retry }, 153, 1_789_555_201_000))))
                .SendAsync(GameplayCommand(), CancellationToken.None);
            Assert.False(retryResult.IsSuccess);
            Assert.Equal(RemoteFailureKind.Dependency, retryResult.Failure.Kind);
            Assert.Equal(TimeSpan.FromSeconds(1), retryResult.Failure.RetryAfter);

            var waiting = new PushWaiting(Operation.Value, 155, 154);
            var waitingResult = await Provider(new ScriptedExecutor(_ => Response(new PushResponse(Stream.Value, new IPushResult[] { waiting }, 153, 1_789_555_201_000))))
                .SendAsync(GameplayCommand(), CancellationToken.None);
            Assert.False(waitingResult.IsSuccess);
            Assert.Equal(RemoteFailureKind.Conflict, waitingResult.Failure.Kind);

            var upgrade = new PushUpgradeRequired(Operation.Value, 155, 2,
                Error("protocol.command_unsupported", "protocol", false));
            var upgradeResult = await Provider(new ScriptedExecutor(_ => Response(new PushResponse(Stream.Value, new IPushResult[] { upgrade }, 153, 1_789_555_201_000))))
                .SendAsync(GameplayCommand(), CancellationToken.None);
            Assert.False(upgradeResult.IsSuccess);
            Assert.Equal(RemoteFailureKind.Unavailable, upgradeResult.Failure.Kind);
        }

        [Fact]
        public async Task OneAuthenticationRefreshResendsExactlyTheSameFrozenMessagePackBody()
        {
            var accepted = new PushAccepted(Operation.Value, 155, 922, new PushGameplayCompletionRecordedResult());
            var executor = new ScriptedExecutor(_ => _.Requests.Count == 0
                ? new HttpResponseData(401, new Dictionary<string, string>(), Array.Empty<byte>())
                : Response(new PushResponse(Stream.Value, new IPushResult[] { accepted }, 155, 1_789_555_201_000)));
            var auth = new AuthSession();

            var result = await Provider(executor, auth).SendAsync(GameplayCommand(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, auth.RefreshCount);
            Assert.Equal(2, executor.Requests.Count);
            Assert.Equal(executor.Requests[0].CopyBody(), executor.Requests[1].CopyBody());
            Assert.Equal("Bearer token-0", executor.Requests[0].Headers["Authorization"]);
            Assert.Equal("Bearer token-1", executor.Requests[1].Headers["Authorization"]);
        }

        [Fact]
        public async Task DeliveryCertaintyAndInvalidIdentityFailClosedWithoutReidentifying()
        {
            var notSent = await Provider(new ThrowingExecutor(HttpDeliveryCertainty.NotSent)).SendAsync(GameplayCommand(), CancellationToken.None);
            Assert.False(notSent.IsSuccess);
            Assert.Equal(RemoteFailureKind.NotSent, notSent.Failure.Kind);

            var uncertain = await Provider(new ThrowingExecutor(HttpDeliveryCertainty.Uncertain)).SendAsync(GameplayCommand(), CancellationToken.None);
            Assert.False(uncertain.IsSuccess);
            Assert.Equal(RemoteFailureKind.OutcomeUncertain, uncertain.Failure.Kind);

            var wrongOperation = new PushAccepted(Guid.Parse("019952d1-0000-7000-8000-000000000004"), 155, 922, new PushGameplayCompletionRecordedResult());
            var identityResult = await Provider(new ScriptedExecutor(_ => Response(new PushResponse(Stream.Value, new IPushResult[] { wrongOperation }, 155, 1_789_555_201_000))))
                .SendAsync(GameplayCommand(), CancellationToken.None);
            Assert.False(identityResult.IsSuccess);
            Assert.Equal(RemoteFailureKind.Protocol, identityResult.Failure.Kind);
        }

        [Fact]
        public async Task UnknownTypeSchemaFingerprintAndBodyAreRejectedBeforeAuthenticationOrNetwork()
        {
            var executor = new ScriptedExecutor(_ => throw new InvalidOperationException("must not send"));
            var auth = new AuthSession();
            var unknown = new RemoteCommand(Operation, Stream, Installation, 155, "unsupported.command", 1, 1,
                1_789_555_200_000, new byte[] { 0x80 }, new byte[32]);

            var unknownResult = await Provider(executor, auth).SendAsync(unknown, CancellationToken.None);
            var badSchema = await Provider(executor, auth).SendAsync(new RemoteCommand(Operation, Stream, Installation, 155,
                "profile.patch", 2, 1, 1_789_555_200_000, ProfileCommand().SemanticBody, new byte[32]), CancellationToken.None);
            var badFingerprint = await Provider(executor, auth).SendAsync(new RemoteCommand(Operation, Stream, Installation, 155,
                "profile.patch", 1, 2, 1_789_555_200_000, ProfileCommand().SemanticBody, new byte[31]), CancellationToken.None);
            var badBody = await Provider(executor, auth).SendAsync(new RemoteCommand(Operation, Stream, Installation, 155,
                "profile.patch", 1, 1, 1_789_555_200_000, new byte[] { 0xc0 }, new byte[32]), CancellationToken.None);

            Assert.Equal(RemoteFailureKind.Protocol, unknownResult.Failure.Kind);
            Assert.Equal(RemoteFailureKind.Protocol, badSchema.Failure.Kind);
            Assert.Equal(RemoteFailureKind.Protocol, badFingerprint.Failure.Kind);
            Assert.Equal(RemoteFailureKind.Protocol, badBody.Failure.Kind);
            Assert.Empty(executor.Requests);
            Assert.Equal(0, auth.GetCount);
        }

        public void Dispose()
        {
            foreach (var refresh in ownedRefresh) refresh.Dispose();
        }

        private CommandPushHttpProvider Provider(IHttpExecutor executor, AuthSession? auth = null)
        {
            var refresh = new AuthRefreshCoordinator();
            ownedRefresh.Add(refresh);
            return new CommandPushHttpProvider(App, Account,
                new BackendHttpConfiguration(new Uri("https://api.example.test/platform"), new BackendNamespace("test")),
                executor, codec, auth ?? new AuthSession(), refresh);
        }

        private RemoteCommand ProfileCommand() => new RemoteCommand(Operation, Stream, Installation, 155, "profile.patch", 1, 1,
            1_789_555_200_000, codec.Encode(new ProfilePatchCommand(5, WireOptional<string>.Present("Ari"), default, default)), new byte[32]);

        private RemoteCommand GameplayCommand() => new RemoteCommand(Operation, Stream, Installation, 155, "gameplay.session.completed", 1, 1,
            1_789_555_200_000, codec.Encode(new GameplayCompletionCommand("level-102", 3, "pocket-bloom", "preview", true, 1,
                new GameplayValidation("opaque-reference", "validation-155"), default, "session-155", 12_500, 1_000,
                new Dictionary<string, long> { ["moves"] = 12, ["bonus"] = -1 })), new byte[32]);

        private static CommonError Error(string code, string category, bool retryable) =>
            new CommonError(code, category, "message", retryable, default, default);

        private HttpResponseData Response(PushResponse value) => new HttpResponseData(200,
            new Dictionary<string, string>
            {
                ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType,
                ["X-Correlation-Id"] = "correlation"
            }, codec.Encode(value));

        private sealed class ScriptedExecutor : IHttpExecutor
        {
            private readonly Func<ScriptedExecutor, HttpResponseData> send;
            public ScriptedExecutor(Func<ScriptedExecutor, HttpResponseData> send) { this.send = send; }
            public List<HttpRequestData> Requests { get; } = new List<HttpRequestData>();
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = send(this);
                Requests.Add(request);
                return Task.FromResult(response);
            }
        }

        private sealed class ThrowingExecutor : IHttpExecutor
        {
            private readonly HttpDeliveryCertainty certainty;
            public ThrowingExecutor(HttpDeliveryCertainty certainty) { this.certainty = certainty; }
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken) =>
                throw new HttpExecutionException(certainty, "test");
        }

        private sealed class AuthSession : IAuthSession
        {
            private long generation;
            private int refreshCount;
            private int getCount;
            public string SessionKey => "command-push-test";
            public int RefreshCount => Volatile.Read(ref refreshCount);
            public int GetCount => Volatile.Read(ref getCount);
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref getCount);
                var current = Volatile.Read(ref generation);
                return Task.FromResult(new AccessTokenSnapshot("token-" + current, current));
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
