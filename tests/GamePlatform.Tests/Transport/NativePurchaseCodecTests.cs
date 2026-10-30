using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Purchases;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http;
namespace GamePlatform.Tests.Transport
{
    public sealed class NativePurchaseCodecTests
    {
        [Fact]
        public async Task AcceptedProofResponseIsDecodedWithoutLoggingProof()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "contracts", "native-purchases", "fixtures.json")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "contracts", "native-purchases", "fixtures.json")));
            var hex = json.RootElement.GetProperty("pendingMessagePackHex").GetString()!;
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            using var refresh = new AuthRefreshCoordinator();
            var provider = new NativePurchaseHttpProvider(new AppId(Guid.Parse("0199f9a0-0000-7000-8000-000000000001")),
                new PlatformUserId(Guid.Parse("0199f9a0-0000-7000-8000-000000000002")),
                new BackendHttpConfiguration(new Uri("https://unit.invalid/"), new BackendNamespace("test")), new Reply(bytes), new Auth(), refresh);
            var result = await provider.SubmitProofAsync(Guid.Parse("0199f9a0-0000-7000-8000-000000000001"),
                new NativeProof("com.example.native", "synthetic.coins", "synthetic-token"), CancellationToken.None);
            Assert.Equal("pending_verification", result.State);
        }
        private sealed class Reply : IHttpExecutor
        {
            private readonly byte[] bytes;
            public Reply(byte[] bytes) => this.bytes = bytes;
            public Task<HttpResponseData> SendAsync(HttpRequestData request, CancellationToken token) =>
                Task.FromResult(new HttpResponseData(202, new Dictionary<string, string>
                {
                    ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType,
                    ["X-Correlation-Id"] = "test"
                }, bytes));
        }
        private sealed class Auth : IAuthSession
        {
            public string SessionKey => "test";
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("test", 1));
            public Task<AccessTokenSnapshot> RefreshAsync(long generation, CancellationToken token) => throw new NotImplementedException();
        }
        [Fact]
        public void ProofLimitCountsUtf8BytesAndRejectsInvalidSurrogates()
        {
            _ = new NativeProof("com.example.native", "synthetic.coins", new string('a', 8192));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NativeProof("com.example.native", "synthetic.coins", new string('é', 4097)));
            Assert.Throws<System.Text.EncoderFallbackException>(() => new NativeProof("com.example.native", "synthetic.coins", "\ud800"));
        }
        [Fact]
        public void ReadsBackendProducedGrantedFixtureAtInt64Maximum()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "contracts", "native-purchases", "fixtures.json")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "contracts", "native-purchases", "fixtures.json")));
            var hex = json.RootElement.GetProperty("grantedMessagePackHex").GetString()!;
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            var result = new NativePurchaseMessagePackCodec().Intent(bytes);
            Assert.Equal("granted", result.State);
            Assert.Equal(long.MaxValue, result.FeedRevision);
            Assert.Equal(long.MaxValue, result.OriginalFeedRevision);
            Assert.Equal(Guid.Parse("0199f9a0-0000-7000-8000-000000000001"), result.OriginatingIntentId);
            var line = Assert.Single(result.ReceiptLines);
            Assert.Equal(long.MaxValue, line.Quantity);
            Assert.Equal(0, line.LineIndex);
            Assert.Equal(1, line.DefinitionVersion);
        }
        [Fact]
        public void EmptyRequestsUseExpectedMessagePack()
        {
            Assert.Equal(new byte[] { 0x80 }, new NativePurchaseMessagePackCodec().ProductsRequest());
        }
        [Fact]
        public void NativeIntentRequestUsesSchemaAndBinaryUuid()
        {
            var bytes = new NativePurchaseMessagePackCodec().IntentRequest(Guid.Parse("0199f9a0-0000-7000-8000-000000000002"), "synthetic.coins", 1);
            Assert.Contains((byte)0xc4, bytes);
            var decoded = new GamePlatform.Serialization.MessagePack.QualificationMessagePackCodec()
                .Decode("native-purchases/schema.json#/$defs/intentRequest", bytes);
            Assert.Equal("0199f9a0-0000-7000-8000-000000000002", decoded.Properties["intentKey"].StringValue);
        }
    }
}
