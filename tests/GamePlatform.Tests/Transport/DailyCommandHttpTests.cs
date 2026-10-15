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
using GamePlatform.Wire.Contracts.Daily;
namespace GamePlatform.Tests.Transport
{
    public sealed class DailyCommandHttpTests : IDisposable
    {
        private static readonly AppId App = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        private static readonly PlatformUserId Account = new PlatformUserId(Guid.Parse("0199f9a0-3000-7777-8888-999999999999"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("test"), App, Account);
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-2000-7777-8888-999999999999"));
        private static readonly OperationId Operation = new OperationId(Guid.Parse("019952d1-0000-7000-8000-000000000003"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-1000-7777-8888-999999999999");
        private static readonly Guid ClaimId = Guid.Parse("019952d1-0000-7000-8000-0000000000d1");
        private readonly StoreQuestMessagePackCodec storeQuest = new StoreQuestMessagePackCodec();
        private readonly AuthRefreshCoordinator refresh = new AuthRefreshCoordinator();

        [Fact]
        public void ProductionFingerprintAcceptsADailyClaimDeterministicallyAndFailsClosedWithoutTheCapability()
        {
            var first = Fingerprint();
            var second = Fingerprint();
            Assert.Equal(first, second);
            Assert.NotEqual(new byte[32], first);
            Assert.NotEqual(first, Fingerprint(Body("2026-10-16")));
            Assert.Throws<NotSupportedException>(() => new CanonicalCommandFingerprint().Compute(Owner, Operation, Stream, Installation, 155, "daily.claim", 1, 1, 1_789_555_200_000, Body(), new byte[32]));
        }

        [Fact]
        public async Task PushSendsTheDailyClaimAndBindsTheDailyClaimedResult()
        {
            var executor = new RoutedExecutor(push: () => Response(storeQuest.EncodePushResponse(new PushResponse(Stream.Value, new IPushResult[] { new PushAccepted(Operation.Value, 155, 42, Claimed("2026-10-15")) }, 155, 1_789_555_201_000))));
            var command = Command(Fingerprint());
            var sent = await Providers(executor, true).ForAccount(App, Account, Installation).CommandPush.SendAsync(command, CancellationToken.None);
            Assert.True(sent.IsSuccess);
            Assert.Equal(RemoteCommandStatus.Accepted, sent.Value!.Status);
            var request = storeQuest.DecodePushRequest(executor.Pushes[0].CopyBody());
            Assert.Equal(("daily.claim", "2026-10-15"), (request.Operations[0].Type, ((DailyClaimCommand)request.Operations[0].Payload).UtcDate));
            var entry = new DailyFeatureCodec().DecodeAcceptedResult(sent.Value.Result!);
            Assert.Equal((ClaimId, "2026-10-15", 3, 2, 25L), (entry.ClaimId, entry.UtcDate, entry.CycleDay, entry.PolicyVersion, entry.Lines[0].Quantity));
        }

        [Fact]
        public async Task DailyClaimedOriginatingFromAnotherInstallationIsBoundByDate()
        {
            var other = new DailyClaimedResult(ClaimId, Guid.Parse("019952d1-0000-7000-8000-0000000000ee"), "2026-10-15", 3, 2, new ILiveSliceRewardLine[] { new LiveSliceCurrencyRewardLine(0, 1, "coins", 25) });
            var executor = new RoutedExecutor(push: () => Response(storeQuest.EncodePushResponse(new PushResponse(Stream.Value, new IPushResult[] { new PushAccepted(Operation.Value, 155, 42, other) }, 155, 1_789_555_201_000))));
            var sent = await Providers(executor, true).ForAccount(App, Account, Installation).CommandPush.SendAsync(Command(Fingerprint()), CancellationToken.None);
            Assert.True(sent.IsSuccess);
            Assert.Equal(RemoteCommandStatus.Accepted, sent.Value!.Status);
            Assert.Equal(ClaimId, new DailyFeatureCodec().DecodeAcceptedResult(sent.Value.Result!).ClaimId);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DailyClaimedForAnotherDateOrWithoutTheCapabilityIsNotAccepted(bool enabled)
        {
            var executor = new RoutedExecutor(push: () => Response(storeQuest.EncodePushResponse(new PushResponse(Stream.Value, new IPushResult[] { new PushAccepted(Operation.Value, 155, 42, Claimed("2026-10-16")) }, 155, 1_789_555_201_000))));
            var sent = await Providers(executor, enabled).ForAccount(App, Account, Installation).CommandPush.SendAsync(Command(new byte[32]), CancellationToken.None);
            Assert.False(sent.IsSuccess);
            Assert.Equal(RemoteFailureKind.Protocol, sent.Failure.Kind);
            Assert.Equal(enabled ? 1 : 0, executor.Pushes.Count);
        }

        [Fact]
        public async Task UncertainDailyClaimDeliveryRecoversThroughTheReceipt()
        {
            var executor = new RoutedExecutor(push: () => throw new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "test"),
                receipt: () => Response(storeQuest.EncodeReceiptResponse(new RecoveryReceiptResponse(Stream.Value, true, new PushAccepted(Operation.Value, 155, 42, Claimed("2026-10-15")), 1_789_555_201_000, Operation.Value, 155))));
            var providers = Providers(executor, true).ForAccount(App, Account, Installation);
            var command = Command(Fingerprint());
            var sent = await providers.CommandPush.SendAsync(command, CancellationToken.None);
            var receipt = await providers.CommandReceipts.LookupAsync(command, CancellationToken.None);
            Assert.Equal(RemoteFailureKind.OutcomeUncertain, sent.Failure.Kind);
            Assert.True(receipt.IsSuccess);
            Assert.Equal(RemoteCommandStatus.Accepted, receipt.Value!.Outcome!.Status);
            Assert.Equal(ClaimId, new DailyFeatureCodec().DecodeAcceptedResult(receipt.Value.Outcome.Result!).ClaimId);
        }

        [Fact]
        public async Task ReceiptBoundToAnotherDateFailsClosed()
        {
            var executor = new RoutedExecutor(receipt: () => Response(storeQuest.EncodeReceiptResponse(new RecoveryReceiptResponse(Stream.Value, true, new PushAccepted(Operation.Value, 155, 42, Claimed("2026-10-16")), 1_789_555_201_000, Operation.Value, 155))));
            var receipt = await Providers(executor, true).ForAccount(App, Account, Installation).CommandReceipts.LookupAsync(Command(Fingerprint()), CancellationToken.None);
            Assert.False(receipt.IsSuccess);
            Assert.Equal(RemoteFailureKind.Protocol, receipt.Failure.Kind);
        }

        public void Dispose() => refresh.Dispose();

        private ProductionBackendHttpProviders Providers(IHttpExecutor executor, bool enabled) =>
            new ProductionBackendHttpProviders(new BackendHttpConfiguration(new Uri("https://api.example.test/platform"), new BackendNamespace("test")), executor, new AuthSession(), refresh, new StoreQuestCapability(enabled));
        private byte[] Fingerprint(byte[]? body = null)
        {
            var destination = new byte[32];
            new CanonicalCommandFingerprint(true).Compute(Owner, Operation, Stream, Installation, 155, "daily.claim", 1, 1, 1_789_555_200_000, body ?? Body(), destination);
            return destination;
        }
        private RemoteCommand Command(byte[] fingerprint) => new RemoteCommand(Operation, Stream, Installation, 155, "daily.claim", 1, 1, 1_789_555_200_000, Body(), fingerprint);
        private static byte[] Body(string utcDate = "2026-10-15") => new DailyFeatureCodec().EncodeClaimCommand(utcDate);
        private static DailyClaimedResult Claimed(string utcDate) => new DailyClaimedResult(ClaimId, Operation.Value, utcDate, 3, 2, new ILiveSliceRewardLine[] { new LiveSliceCurrencyRewardLine(0, 1, "coins", 25) });
        private static HttpResponseData Response(byte[] body) => new HttpResponseData(200, new Dictionary<string, string> { ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType, ["X-Correlation-Id"] = "correlation" }, body);
        private sealed class RoutedExecutor : IHttpExecutor
        {
            private readonly Func<HttpResponseData>? push;
            private readonly Func<HttpResponseData>? receipt;
            public RoutedExecutor(Func<HttpResponseData>? push = null, Func<HttpResponseData>? receipt = null)
            {
                this.push = push;
                this.receipt = receipt;
            }
            public List<HttpRequestData> Pushes { get; } = new List<HttpRequestData>();
            public List<HttpRequestData> Receipts { get; } = new List<HttpRequestData>();
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
            {
                var isReceipt = request.Uri.EndsWith("/sync/receipts/lookup", StringComparison.Ordinal);
                (isReceipt ? Receipts : Pushes).Add(request);
                var send = isReceipt ? receipt : push;
                if (send == null)
                    throw new InvalidOperationException("Unexpected route.");
                return Task.FromResult(send());
            }
        }
        private sealed class AuthSession : IAuthSession
        {
            public string SessionKey => "daily-command-test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken) => Task.FromResult(new AccessTokenSnapshot("token-0", 0));
            public Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken) => GetAsync(cancellationToken);
        }
    }
}
