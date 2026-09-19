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

namespace GamePlatform.Tests.Serialization
{
    public sealed class DesktopQualificationWireCodecTests
    {
        private const string ProvisionRequestHex = "83af70726f746f636f6c56657273696f6e01ae696e7374616c6c6174696f6e4964c41000112233445546778899aabbccddeeffae636c69656e7453747265616d4964c41001890f3e7a6b7c8d9e0f102030405060";
        private const string ProvisionResponseHex = "86af70726f746f636f6c56657273696f6e01a76163636f756e7483ae706c6174666f726d557365724964c41000112233445546778899aabbccddeeffa9637265617465644174cf000001a0a9cbc960a87265766973696f6e01aa6d656d6265727368697084a56170704964c41001890f3e7a6b7c8d9e0f102030405060a6737461747573a6616374697665a9637265617465644174cf000001a0a9cbc960a87265766973696f6e01ae636c69656e7453747265616d4964c41001890f3e7a6b7c8d9e0f102030405060ac6e65787453657175656e636501aa73657276657254696d65cf000001a0a9cd53e8";

        [Fact]
        public void CandidateMatchesApprovedProducerBytes()
        {
            var codec = new DesktopQualificationWireCodec();
            Assert.DoesNotContain(typeof(IWireCodec), typeof(DesktopQualificationWireCodec).GetInterfaces());
            var request = new ProvisionRequest(
                Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"),
                Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));

            Assert.Equal(ProvisionRequestHex, Convert.ToHexString(codec.Encode(request)).ToLowerInvariant());

            var response = codec.Decode<ProvisionResponse>(Convert.FromHexString(ProvisionResponseHex));
            Assert.Equal(Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"), response.Account.PlatformUserId);
            Assert.Equal(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"), response.ClientStreamId);
            Assert.Equal(1, response.NextSequence);
            Assert.Equal(1_789_555_201_000L, response.ServerTime);
        }

        [Fact]
        public void InterfaceUnionRoundTripsWithOpaqueBinaryCursorAndSigned64Values()
        {
            var codec = new DesktopQualificationWireCodec();
            var token = Convert.ToBase64String(Enumerable.Range(0, 16).Select(value => (byte)value).ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            IPullResponse value = new PullPage(
                new[] { new PullGroup(long.MaxValue, new IProjectionChange[]
                {
                    new ProjectionWalletUpsert(new ProjectionWalletKey("coins"), long.MaxValue, new ProjectionWalletData(long.MaxValue))
                }) },
                token, false, long.MaxValue, 253_402_300_799_999L);

            var encoded = codec.Encode(value);
            var decoded = codec.Decode<IPullResponse>(encoded);

            var page = Assert.IsType<PullPage>(decoded);
            var group = Assert.Single(page.Changes);
            var change = Assert.IsType<ProjectionWalletUpsert>(Assert.Single(group.Changes));
            Assert.Equal(long.MaxValue, group.FeedRevision);
            Assert.Equal(long.MaxValue, change.Data.Balance);
            Assert.Equal(token, page.NextCursor);
            Assert.Equal(encoded, codec.Encode<IPullResponse>(decoded));
        }

        [Fact]
        public void PreservesAbsentAndExplicitNull()
        {
            var codec = new DesktopQualificationWireCodec();
            var value = new ProfilePatchCommand(long.MaxValue, default, WireOptional<string?>.Present(null), default);

            var decoded = codec.Decode<ProfilePatchCommand>(codec.Encode(value));

            Assert.False(decoded.DisplayName.IsPresent);
            Assert.True(decoded.AvatarKey.IsPresent);
            Assert.Null(decoded.AvatarKey.Value);
            Assert.Equal(long.MaxValue, decoded.ExpectedRevision);
        }

        [Fact]
        public void RejectsUnsupportedTypesNullBodiesAndUnreviewedFields()
        {
            var codec = new DesktopQualificationWireCodec();
            Assert.Throws<QualificationCodecException>(() => codec.Encode(42));
            Assert.Throws<ArgumentNullException>(() => codec.Decode<ProvisionResponse>(null!));

            var original = Convert.FromHexString(ProvisionRequestHex);
            var withUnknownField = new byte[] { 0x84 }.Concat(original.Skip(1)).Concat(new byte[] { 0xa1, (byte)'x', 0xc0 }).ToArray();
            Assert.Throws<QualificationCodecException>(() => codec.Decode<ProvisionRequest>(withUnknownField));
        }

        [Fact]
        public void RetainsHostileInputBoundsAndDuplicateKeyRejection()
        {
            IWireCodec codec = QualificationPort();
            Assert.Throws<QualificationCodecException>(() => codec.Decode<ProvisionRequest>(new byte[262_145]));

            var nested = Enumerable.Repeat((byte)0x91, 33).Concat(new byte[] { 0xc0 }).ToArray();
            Assert.Throws<QualificationCodecException>(() => codec.Decode<ProvisionRequest>(nested));

            var duplicateProtocolVersion = Convert.FromHexString("82af70726f746f636f6c56657273696f6e01af70726f746f636f6c56657273696f6e01");
            Assert.Throws<QualificationCodecException>(() => codec.Decode<ProvisionRequest>(duplicateProtocolVersion));
        }

        [Fact]
        public async Task TestOnlyBridgeFitsProvisioningProvider()
        {
            IWireCodec codec = QualificationPort();
            var app = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
            var installation = Guid.Parse("00112233-4455-4677-8899-aabbccddeeff");
            var stream = new ClientStreamId(Guid.Parse("019952d1-0000-7000-8000-000000000003"));
            var executor = new InlineExecutor(request =>
            {
                var decoded = codec.Decode<ProvisionRequest>(request.CopyBody());
                Assert.Equal(installation, decoded.InstallationId);
                Assert.Equal(stream.Value, decoded.ClientStreamId);
                var response = new ProvisionResponse(
                    new CommonAccount(Guid.Parse("019952d1-0000-7000-8000-000000000004"), 1, 1),
                    new CommonMembership(app.Value, "active", 1, 1), stream.Value, 1, 1);
                return Response(ProvisioningHttpProvider.MediaType, codec.Encode(response));
            });
            using var refresh = new AuthRefreshCoordinator();
            var provider = new ProvisioningHttpProvider(Configuration(), executor, codec, new StaticAuth(), refresh);

            var result = await provider.ProvisionAsync(app, installation, stream, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(stream, result.Value!.StreamId);
            Assert.Single(executor.Requests);
        }

        [Fact]
        public async Task TestOnlyBridgeFitsPrivateBootstrapWithoutJsonFallback()
        {
            var candidate = new DesktopQualificationWireCodec();
            IWireCodec codec = new TestOnlyQualificationPortAdapter(candidate);
            var app = new AppId(Guid.Parse("01890f3e-7a6b-7c8d-9e0f-102030405060"));
            var account = new PlatformUserId(Guid.Parse("019952d1-0000-7000-8000-000000000004"));
            var stream = new ClientStreamId(Guid.Parse("019952d1-0000-7000-8000-000000000003"));
            var installation = Guid.Parse("00112233-4455-4677-8899-aabbccddeeff");
            var token = Convert.ToBase64String(Enumerable.Repeat((byte)7, 12).ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var response = new BootstrapStartResponse(
                new CommonAccount(account.Value, 1, 1), new CommonMembership(app.Value, "active", 1, 1),
                token, 9, 2, Guid.Parse("019952d1-0000-7000-8000-000000000005"),
                new[] { "profile", "progression", "inventory", "wallet", "entitlements" }.Select(name => new BootstrapCollection(name)).ToArray(), token, 100, 10,
                new CommonStreamState(stream.Value, installation, 7, 8, "active"));
            var executor = new InlineExecutor(_ => Response(PrivateSyncHttpProvider.MessagePackMediaType, candidate.Encode(response)));
            using var refresh = new AuthRefreshCoordinator();
            var provider = new PrivateSyncHttpProvider(app, account, Configuration(), executor, codec, new StaticAuth(), refresh);

            var result = await provider.StartBootstrapAsync(stream, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(12, result.Value!.CopySession().Length);
            Assert.Equal(stream, result.Value.StreamId);
            Assert.DoesNotContain(executor.Requests.Single().Headers.Values, value => value.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static BackendHttpConfiguration Configuration() =>
            new BackendHttpConfiguration(new Uri("https://api.example.test/platform"), new BackendNamespace("test"));

        private static IWireCodec QualificationPort() =>
            new TestOnlyQualificationPortAdapter(new DesktopQualificationWireCodec());

        private static HttpResponseData Response(string mediaType, byte[] body) =>
            new HttpResponseData(200, new Dictionary<string, string>
            {
                ["Content-Type"] = mediaType,
                ["X-Correlation-Id"] = "codec-desktop-test"
            }, body);

        private sealed class InlineExecutor : IHttpExecutor
        {
            private readonly Func<HttpRequestData, HttpResponseData> send;
            public InlineExecutor(Func<HttpRequestData, HttpResponseData> send) { this.send = send; }
            public List<HttpRequestData> Requests { get; } = new List<HttpRequestData>();
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                return Task.FromResult(send(request));
            }
        }

        /// <summary>
        /// Test-only bridge proving that the desktop candidate fits the provider
        /// contract. Production source deliberately supplies no such registration.
        /// </summary>
        private sealed class TestOnlyQualificationPortAdapter : IWireCodec
        {
            private readonly DesktopQualificationWireCodec candidate;
            public TestOnlyQualificationPortAdapter(DesktopQualificationWireCodec candidate) { this.candidate = candidate; }
            public byte[] Encode<T>(T value) => candidate.Encode(value);
            public T Decode<T>(byte[] payload) => candidate.Decode<T>(payload);
        }

        private sealed class StaticAuth : IAuthSession
        {
            public string SessionKey => "codec-desktop-test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new AccessTokenSnapshot("test-token", 0));
            public Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken) =>
                Task.FromResult(new AccessTokenSnapshot("test-token", 0));
        }
    }
}
