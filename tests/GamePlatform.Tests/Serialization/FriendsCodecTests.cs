#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Wire.Contracts;
using GamePlatform.Wire.Contracts.Friends;
using GamePlatform.Wire.Contracts.Teams;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
namespace GamePlatform.Tests.Serialization
{
    public sealed class FriendsCodecTests
    {
        [Fact]
        public void CanonicalBackendFixturesRoundTripAndEmitExactSdkPeerBytes()
        {
            string root = Root();
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "contracts/friends/fixtures.json")));
            var codec = new QualificationMessagePackCodec();
            var typed = new FriendsMessagePackCodec();
            var rows = new List<object>();
            var peerPath = Environment.GetEnvironmentVariable("FRIENDS_PEER_FIXTURES");
            using var peers = peerPath == null ? null : JsonDocument.Parse(File.ReadAllText(peerPath));
            foreach (var fixture in document.RootElement.GetProperty("fixtures").EnumerateArray())
            {
                string def = fixture.GetProperty("schema").GetString()!, schema = "friends.schema.json#/$defs/" + def;
                var bytes = codec.Encode(schema, Parse(fixture.GetProperty("value")));
                var inputBytes = bytes;
                if (peers != null)
                {
                    var peerFixture = peers.RootElement.GetProperty("fixtures").EnumerateArray().Single(x => x.GetProperty("name").GetString() == fixture.GetProperty("name").GetString());
                    var foreignBytes = Convert.FromHexString(peerFixture.GetProperty("messagePackHex").GetString()!);
                    Assert.Equal(bytes, codec.Encode(schema, codec.Decode(schema, foreignBytes)));
                    inputBytes = foreignBytes;
                }
                Assert.Equal(bytes, codec.Encode(schema, codec.Decode(schema, bytes)));
                if (def == "response")
                    Assert.Equal(bytes, typed.EncodeResponse(typed.DecodeResponse(inputBytes)));
                if (def == "queryRequest")
                    Assert.Equal(bytes, typed.EncodeQuery(typed.DecodeQuery(inputBytes)));
                if (def == "commandRequest")
                    Assert.Equal(bytes, typed.EncodeCommand(typed.DecodeCommand(inputBytes)));
                if (def == "rankingQueryResponse")
                {
                    var value = typed.DecodeRankingResponse(inputBytes);
                    Assert.Equal(0, value.Self!.Score);
                    Assert.Equal(bytes, typed.EncodeRankingResponse(value));
                }
                if (fixture.TryGetProperty("messagePackHex", out var peer))
                    Assert.Equal(bytes, Convert.FromHexString(peer.GetString()!));
                rows.Add(new
                {
                    name = fixture.GetProperty("name").GetString(),
                    schema,
                    messagePackHex = Convert.ToHexString(bytes).ToLowerInvariant()
                });
            }
            string output = Path.Combine(root, "artifacts/friends");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "sdk-fixtures.json"), JsonSerializer.Serialize(new
            {
                protocol = "friends-1.0.0",
                fixtures = rows
            }));
        }
        [Fact]
        public void ClosedFriendsCodecRejectsDuplicateUnknownFieldsAndIllegalPayloads()
        {
            var codec = new FriendsMessagePackCodec();
            var query = codec.EncodeQuery(new FriendsQuery("mine"));
            Assert.Equal(0x83, query[0]);
            var duplicate = new byte[] { 0x84 }.Concat(query.Skip(1)).Concat(new byte[] { 0xa4 }).Concat(Encoding.ASCII.GetBytes("view")).Concat(new byte[] { 0xa4 }).Concat(Encoding.ASCII.GetBytes("mine")).ToArray();
            Assert.Throws<QualificationCodecException>(() => codec.DecodeQuery(duplicate));
            Assert.Throws<QualificationCodecException>(() => new QualificationMessagePackCodec().Encode("friends.schema.json#/$defs/queryRequest", V.Object(new[] { Pair("protocolVersion", V.Integer(1)), Pair("view", V.String("lookup")), Pair("pageSize", V.Integer(30)), Pair("targetPlayerId", V.String("019a17aa-1000-7000-8000-000000000001")) })));
            Assert.Throws<ArgumentException>(() => new FriendsCommand(Guid.NewGuid(), "set_privacy", new FriendsCommandPayload(allowRequests: true)));
            Assert.Throws<ArgumentException>(() => new FriendsCommand(Guid.Parse("019a17aa-1000-7000-8000-000000000001"), "send", new FriendsCommandPayload("01ABCDEFGHJK", Guid.NewGuid())));
            Assert.Throws<ArgumentException>(() => new FriendsQuery("lookup", "invalid"));
        }
        [Fact]
        public void CombinedPullPreservesCoreTeamsAndFriendsChanges()
        {
            var id = "019a17aa-1000-7000-8000-000000000001";
            V Marker(string family, string key) => V.Object(new[] { Pair("entityType", V.String(family)), Pair("entityKey", V.Object(new[] { Pair(key, V.String(id)) })), Pair("revision", V.String("1")), Pair("kind", V.String("invalidation")) });
            var wallet = V.Object(new[] { Pair("entityType", V.String("wallet")), Pair("entityKey", V.Object(new[] { Pair("currencyId", V.String("coin")) })), Pair("revision", V.String("1")), Pair("kind", V.String("invalidation")) });
            var group = V.Object(new[] { Pair("feedRevision", V.String("1")), Pair("changes", V.Array(new[] { wallet, Marker("teams", "teamId"), Marker("friends", "playerId") })) });
            var value = V.Object(new[] { Pair("protocolVersion", V.Integer(1)), Pair("resetRequired", V.Boolean(false)), Pair("changes", V.Array(new[] { group })), Pair("nextCursor", V.String("AAAAAAAAAAAAAAAA")), Pair("hasMore", V.Boolean(false)), Pair("committedThrough", V.String("1")), Pair("serverTime", V.Integer(1790000000000)) });
            var bytes = new QualificationMessagePackCodec().Encode("friends.schema.json#/$defs/pullResponse", value);
            var page = Assert.IsType<PullPage>(new FriendsSupportingWireCodec().Decode<IPullResponse>(bytes));
            Assert.IsType<ProjectionWalletRemoval>(page.Changes[0].Changes[0]);
            Assert.IsType<TeamsProjectionInvalidation>(page.Changes[0].Changes[1]);
            Assert.IsType<FriendsProjectionInvalidation>(page.Changes[0].Changes[2]);
        }
        internal static string Root()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "GamePlatform.sln")))
                root = root.Parent;
            return root?.FullName ?? throw new InvalidOperationException("Missing source root.");
        }
        internal static V Parse(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Object => V.Object(value.EnumerateObject().Select(p => Pair(p.Name, Parse(p.Value)))),
            JsonValueKind.Array => V.Array(value.EnumerateArray().Select(Parse)),
            JsonValueKind.String => V.String(value.GetString()!),
            JsonValueKind.Number => V.Integer(value.GetInt64()),
            JsonValueKind.Null => V.Null,
            JsonValueKind.True => V.Boolean(true),
            JsonValueKind.False => V.Boolean(false),
            _ => throw new InvalidOperationException()
        };
        private static KeyValuePair<string, V> Pair(string key, V value) => new KeyValuePair<string, V>(key, value);
    }
}
