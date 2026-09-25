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
    public sealed class StoreQuestHttpCapabilityTests : IDisposable
    {
        private static readonly AppId App = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
        private static readonly PlatformUserId Account = new PlatformUserId(Guid.Parse("0199f9a0-3000-7777-8888-999999999999"));
        private static readonly OwnerScope Owner = new OwnerScope(new BackendNamespace("test"), App, Account);
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-2000-7777-8888-999999999999"));
        private static readonly OperationId Operation = new OperationId(Guid.Parse("019952d1-0000-7000-8000-000000000003"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-1000-7777-8888-999999999999");
        private static readonly Guid PurchaseKey = Guid.Parse("019952d1-0000-7000-8000-0000000000a1");
        private readonly StoreQuestMessagePackCodec storeQuest = new StoreQuestMessagePackCodec();
        private readonly MessagePackWireCodec codec = new MessagePackWireCodec();
        private readonly AuthRefreshCoordinator refresh = new AuthRefreshCoordinator();

        [Fact]
        public async Task UncertainPurchaseDeliveryResolvesThroughTheA06ReceiptSuperset()
        {
            var executor = new RoutedExecutor(push: () => throw new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "test"),
                receipt: () => Response(storeQuest.EncodeReceiptResponse(Receipt(new PushAccepted(Operation.Value, 155, 42, Purchased(PurchaseKey))))));
            var providers = Providers(executor, true).ForAccount(App, Account, Installation);
            var command = PurchaseCommand(Fingerprint(true));

            var sent = await providers.CommandPush.SendAsync(command, CancellationToken.None);
            var receipt = await providers.CommandReceipts.LookupAsync(command, CancellationToken.None);

            Assert.Equal(RemoteFailureKind.OutcomeUncertain, sent.Failure.Kind);
            Assert.True(receipt.IsSuccess);
            Assert.True(receipt.Value!.Found);
            Assert.Equal(RemoteCommandStatus.Accepted, receipt.Value.Outcome!.Status);
            var terminal = Assert.IsType<PushAccepted>(storeQuest.DecodeResult(receipt.Value.Outcome.Result!));
            Assert.Equal(PurchaseKey, Assert.IsType<StoreOfferPurchasedResult>(terminal.Result).PurchaseKey);
            var lookup = codec.Decode<RecoveryReceiptRequest>(executor.Receipts[0].CopyBody());
            Assert.Equal(Convert.ToHexString(command.Fingerprint).ToLowerInvariant(), lookup.Fingerprint);
        }

        [Fact]
        public async Task FoundStoreRejectionIsTerminal()
        {
            var rejected = new PushTerminalRejected(Operation.Value, 155, new CommonError("store_insufficient_balance", "conflict", "message", false, default, default));
            var executor = new RoutedExecutor(receipt: () => Response(storeQuest.EncodeReceiptResponse(Receipt(rejected))));

            var receipt = await Providers(executor, true).ForAccount(App, Account, Installation).CommandReceipts.LookupAsync(PurchaseCommand(Fingerprint(true)), CancellationToken.None);

            Assert.Equal(RemoteCommandStatus.TerminalRejected, receipt.Value!.Outcome!.Status);
            Assert.IsType<PushTerminalRejected>(storeQuest.DecodeResult(receipt.Value.Outcome.Result!));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task A06ReceiptsFailClosedWhenDisabledOrBoundToAnotherPurchase(bool enabled)
        {
            var other = Purchased(Guid.Parse("019952d1-0000-7000-8000-0000000000a2"));
            var body = storeQuest.EncodeReceiptResponse(Receipt(new PushAccepted(Operation.Value, 155, 42, enabled ? other : Purchased(PurchaseKey))));
            var executor = new RoutedExecutor(receipt: () => Response(body));

            var receipt = await Providers(executor, enabled).ForAccount(App, Account, Installation).CommandReceipts.LookupAsync(PurchaseCommand(new byte[32]), CancellationToken.None);

            Assert.Equal(RemoteFailureKind.Protocol, receipt.Failure.Kind);
        }

        [Fact]
        public async Task LegacyReceiptBytesAreUnchangedByTheCapability()
        {
            var body = codec.Encode(Receipt(new PushAccepted(Operation.Value, 155, 42, new PushGameplayCompletionRecordedResult())));
            var legacy = new RemoteCommand(Operation, Stream, Installation, 155, "gameplay.session.completed", 1, 1, 1_789_555_200_000, new byte[] { 8 }, new byte[32]);

            var off = await Providers(new RoutedExecutor(receipt: () => Response(body)), false).ForAccount(App, Account, Installation).CommandReceipts.LookupAsync(legacy, CancellationToken.None);
            var on = await Providers(new RoutedExecutor(receipt: () => Response(body)), true).ForAccount(App, Account, Installation).CommandReceipts.LookupAsync(legacy, CancellationToken.None);

            Assert.True(off.IsSuccess, off.Failure.Kind.ToString());
            Assert.True(on.IsSuccess, on.Failure.Kind.ToString());
            Assert.Equal(off.Value!.Outcome!.Result, on.Value!.Outcome!.Result);
        }

        [Fact]
        public async Task A06ResultForALegacyCommandIsAProtocolFailure()
        {
            var body = storeQuest.EncodeReceiptResponse(Receipt(new PushAccepted(Operation.Value, 155, 42, Purchased(PurchaseKey))));
            var legacy = new RemoteCommand(Operation, Stream, Installation, 155, "profile.patch", 1, 1, 1_789_555_200_000, new byte[] { 8 }, new byte[32]);

            var receipt = await Providers(new RoutedExecutor(receipt: () => Response(body)), true).ForAccount(App, Account, Installation).CommandReceipts.LookupAsync(legacy, CancellationToken.None);

            Assert.Equal(RemoteFailureKind.Protocol, receipt.Failure.Kind);
        }

        [Fact]
        public async Task OneSwitchKeepsFingerprintPushAndReceiptConsistentWhenDisabled()
        {
            var executor = new RoutedExecutor();
            var providers = Providers(executor, false);

            Assert.Throws<NotSupportedException>(() => Fingerprint(false));
            var sent = await providers.ForAccount(App, Account, Installation).CommandPush.SendAsync(PurchaseCommand(new byte[32]), CancellationToken.None);

            Assert.Equal(RemoteFailureKind.Protocol, sent.Failure.Kind);
            Assert.Empty(executor.Pushes);
            Assert.IsType<CanonicalCommandFingerprint>(providers.CommandFingerprint);
        }

        [Fact]
        public void TheEnabledReceiptSupersetRequiresMessagePack()
        {
            Assert.Throws<ArgumentException>(() => new CommandReceiptHttpProvider(App, Account, Installation, Configuration(), new RoutedExecutor(), codec,
                new AuthSession(), refresh, BackendWireRepresentation.DiagnosticJson, new StoreQuestCapability(true)));
        }

        public void Dispose() => refresh.Dispose();

        private ProductionBackendHttpProviders Providers(IHttpExecutor executor, bool enabled) =>
            new ProductionBackendHttpProviders(Configuration(), executor, new AuthSession(), refresh, new StoreQuestCapability(enabled));

        private byte[] Fingerprint(bool enabled)
        {
            var destination = new byte[32];
            Providers(new RoutedExecutor(), enabled).CommandFingerprint.Compute(Owner, Operation, Stream, Installation, 155, "store.offer.purchase", 1, 1, 1_789_555_200_000, PurchaseBody(), destination);
            return destination;
        }

        private RemoteCommand PurchaseCommand(byte[] fingerprint) =>
            new RemoteCommand(Operation, Stream, Installation, 155, "store.offer.purchase", 1, 1, 1_789_555_200_000, PurchaseBody(), fingerprint);

        private byte[] PurchaseBody() => storeQuest.EncodePurchaseCommand(new StoreOfferPurchaseCommand("test.offer.starter", 1, PurchaseKey));

        private static StoreOfferPurchasedResult Purchased(Guid key) => new StoreOfferPurchasedResult(Guid.Parse("019952d1-0000-7000-8000-0000000000d0"), Operation.Value,
            "test.offer.starter", 1, key, new StoreOfferDebit("test.coin", 10), new ILiveSliceRewardLine[] { new LiveSliceStackRewardLine(0, 1, "test.item.hat", 1) });

        private static RecoveryReceiptResponse Receipt(IPushFinalResult result) => new RecoveryReceiptResponse(Stream.Value, true, result, 1_789_555_201_000, Operation.Value, 155);

        private static BackendHttpConfiguration Configuration() => new BackendHttpConfiguration(new Uri("https://api.example.test/platform"), new BackendNamespace("test"));

        private static HttpResponseData Response(byte[] body) => new HttpResponseData(200,
            new Dictionary<string, string> { ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType, ["X-Correlation-Id"] = "correlation" }, body);

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
                if (send == null) throw new InvalidOperationException("Unexpected route.");
                return Task.FromResult(send());
            }
        }

        private sealed class AuthSession : IAuthSession
        {
            public string SessionKey => "store-quest-test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken) => Task.FromResult(new AccessTokenSnapshot("token-0", 0));
            public Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken) => GetAsync(cancellationToken);
        }
    }
}
