#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GamePlatform.Features.Leaderboards;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Tests.Features.Leaderboards;
using GamePlatform.Transport.Http;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
namespace GamePlatform.Tests.Serialization
{
    public sealed class LeaderboardsCodecTests
    {
        [Fact]
        public void Signed64AndSnapshotSelfRoundTripWithoutDoublePrecisionLoss()
        {
            var codec = new LeaderboardsStateCodec();
            var value = LeaderboardsServiceTests.Response(score: long.MaxValue);
            var bytes = codec.EncodeResponse(value);
            var decoded = codec.DecodeResponse(bytes);
            Assert.Equal(long.MaxValue, decoded.Entries[0].Score);
            Assert.Null(decoded.Self);
            Assert.Equal(bytes, codec.EncodeResponse(decoded));
            Assert.Throws<QualificationCodecException>(() => codec.DecodeResponse(bytes.Concat(new byte[] { 0 }).ToArray()));
        }
        [Fact]
        public void SeparateBackendLogicalFixturesEmitSdkMessagePackForPeerExchange()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "GamePlatform.sln")))
                root = root.Parent;
            Assert.NotNull(root);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName, "contracts/leaderboards/fixtures.json")));
            var codec = new QualificationMessagePackCodec();
            var rows = new List<object>();
            foreach (var fixture in document.RootElement.GetProperty("fixtures").EnumerateArray())
            {
                string schema = "leaderboards.schema.json#/$defs/" + fixture.GetProperty("schema").GetString();
                var bytes = codec.Encode(schema, Parse(fixture.GetProperty("value")));
                Assert.Equal(bytes, codec.Encode(schema, codec.Decode(schema, bytes)));
                if (fixture.TryGetProperty("messagePackHex", out var foreign))
                    Assert.Equal(bytes, codec.Encode(schema, codec.Decode(schema, Convert.FromHexString(foreign.GetString()!))));
                rows.Add(new
                {
                    name = fixture.GetProperty("name").GetString(),
                    schema,
                    messagePackHex = Convert.ToHexString(bytes).ToLowerInvariant()
                });
            }
            string output = Path.Combine(root.FullName, "artifacts/leaderboards");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "sdk-fixtures.json"), JsonSerializer.Serialize(new
            {
                protocol = "leaderboards-1.0.0",
                fixtures = rows
            }));
        }
        [Fact]
        public void ClosedSchemaRejectsUnknownFieldAndIllegalCountryScope()
        {
            var codec = new QualificationMessagePackCodec();
            Assert.Throws<QualificationCodecException>(() => codec.Encode("leaderboards.schema.json#/$defs/queryRequest", V.Object(new[] {
                new KeyValuePair<string,V>("boardId",V.String("players-monthly")),new KeyValuePair<string,V>("scope",V.String("global")),
                new KeyValuePair<string,V>("limit",V.Integer(30)),new KeyValuePair<string,V>("unexpected",V.Boolean(true)) })));
            Assert.Throws<ArgumentException>(() => new LeaderboardsQuery("players-monthly", "global", "DE"));
            Assert.Throws<ArgumentException>(() => new LeaderboardsCountrySelection("DE", "FR", null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LeaderboardsQuery("players-monthly", cursor: "short"));
        }
        [Fact]
        public void UnicodeScalarsAndExactInt64AssignmentTimeSurviveTypedCodec()
        {
            string name = string.Concat(Enumerable.Repeat("🌼", 64));
            var entry = new LeaderboardEntry(Guid.Parse("019a17aa-1000-7000-8000-000000000001"), 1, long.MaxValue, name);
            Assert.Equal(name, entry.DisplayName);
            Assert.Throws<ArgumentOutOfRangeException>(() => new LeaderboardEntry(entry.ParticipantId, 1, 1, name + "x"));
            var codec = new LeaderboardsMessagePackCodec();
            var value = new GamePlatform.Wire.Contracts.Leaderboards.LeaderboardsCountryResponse(entry.ParticipantId, "players-monthly", "player", entry.ParticipantId, "DE", long.MaxValue);
            Assert.Equal(long.MaxValue, codec.DecodeCountryResponse(codec.EncodeCountryResponse(value)).EffectiveAt);
        }
        private static V Parse(JsonElement x) => x.ValueKind switch
        {
            JsonValueKind.Object => V.Object(x.EnumerateObject().Select(p => new KeyValuePair<string, V>(p.Name, Parse(p.Value)))),
            JsonValueKind.Array => V.Array(x.EnumerateArray().Select(Parse)),
            JsonValueKind.String => V.String(x.GetString()!),
            JsonValueKind.Number => V.Integer(x.GetInt64()),
            JsonValueKind.Null => V.Null,
            JsonValueKind.True => V.Boolean(true),
            JsonValueKind.False => V.Boolean(false),
            _ => throw new InvalidOperationException()
        };
    }
}
