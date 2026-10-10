#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using GamePlatform.Core;
using GamePlatform.Features.Teams;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Tests.Features.Teams;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;
using GamePlatform.Wire.Contracts.Teams;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;

namespace GamePlatform.Tests.Serialization
{
    public sealed class TeamsCodecTests
    {
        [Fact]
        public void BackendProducedGoldenMapsRoundTripSemanticallyAndEmitIndependentSdkBytes()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "GamePlatform.sln"))) root = root.Parent;
            Assert.NotNull(root);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName, "contracts/teams/fixtures.json")));
            var codec = new QualificationMessagePackCodec();
            var emitted = new List<object>();
            foreach (var fixture in document.RootElement.GetProperty("fixtures").EnumerateArray())
            {
                string schema = fixture.GetProperty("schema").GetString()!.Replace("schemas/", "");
                byte[] foreign = Convert.FromHexString(fixture.GetProperty("messagePackHex").GetString()!);
                var decoded = codec.Decode(schema, foreign);
                byte[] own = codec.Encode(schema, Parse(fixture.GetProperty("diagnostic")));
                Assert.Equal(own, codec.Encode(schema, decoded));
                if (fixture.GetProperty("name").GetString() == "mine-response")
                    Assert.Equal(9_007_199_254_740_993, new TeamsStateCodec().DecodeResponse(foreign).Membership!.HelpCount);
                if (fixture.GetProperty("name").GetString() == "send-message")
                    Assert.Equal(TeamsServiceTests.Operation, new TeamsStateCodec().DecodeCommand(foreign).OperationId.Value);
                emitted.Add(new { name = fixture.GetProperty("name").GetString(), schema = fixture.GetProperty("schema").GetString(), diagnostic = fixture.GetProperty("diagnostic").Clone(), messagePackHex = Convert.ToHexString(own).ToLowerInvariant() });
            }
            string output = Path.Combine(root.FullName, "artifacts", "teams");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "sdk-fixtures.json"), JsonSerializer.Serialize(new { protocol = "teams-1.0.0", fixtures = emitted }));
        }

        private static V Parse(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Object => V.Object(value.EnumerateObject().Select(p => new KeyValuePair<string, V>(p.Name, Parse(p.Value)))),
            JsonValueKind.Array => V.Array(value.EnumerateArray().Select(Parse)),
            JsonValueKind.String => V.String(value.GetString()!),
            JsonValueKind.Number => V.Integer(value.GetInt64()),
            JsonValueKind.True => V.Boolean(true), JsonValueKind.False => V.Boolean(false), JsonValueKind.Null => V.Null,
            _ => throw new InvalidOperationException()
        };

        [Fact]
        public void ResponseRetainsExactSigned64AndRfcIdentity()
        {
            var codec = new TeamsStateCodec();
            var bytes = codec.EncodeResponse(TeamsServiceTests.Response());
            var value = codec.DecodeResponse(bytes);
            Assert.Equal(9_007_199_254_740_993, value.Membership!.HelpCount);
            Assert.Equal(TeamsServiceTests.Team, value.Team!.TeamId);
            Assert.Equal(bytes, codec.EncodeResponse(value));
            Assert.Throws<QualificationCodecException>(() => codec.DecodeResponse(bytes.Concat(new byte[] { 0 }).ToArray()));
        }

        [Theory]
        [InlineData("join")]
        [InlineData("request_join")]
        [InlineData("cancel_request")]
        [InlineData("leave")]
        [InlineData("request_help")]
        public void EmptyPayloadCommandsRoundTrip(string action)
        {
            var codec = new TeamsStateCodec();
            var command = new GamePlatform.Features.Teams.TeamsCommand(new OperationId(TeamsServiceTests.Operation), action, TeamsServiceTests.Team);
            var bytes = codec.EncodeCommand(command);
            Assert.Equal(bytes, codec.EncodeCommand(codec.DecodeCommand(bytes)));
        }

        [Fact]
        public void InvalidCommandShapeAndOverflowFailBeforeTransport()
        {
            var codec = new TeamsStateCodec();
            Assert.Throws<QualificationCodecException>(() => codec.EncodeCommand(new GamePlatform.Features.Teams.TeamsCommand(new OperationId(TeamsServiceTests.Operation), "invented", TeamsServiceTests.Team)));
            Assert.Throws<QualificationCodecException>(() => codec.EncodeCommand(new GamePlatform.Features.Teams.TeamsCommand(new OperationId(TeamsServiceTests.Operation), "send_message", TeamsServiceTests.Team,
                payload: new GamePlatform.Features.Teams.TeamsCommandPayload(text: new string('x', 3000)))));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GamePlatform.Features.Teams.TeamsQuery("discover", pageSize: 51));
        }

        [Fact]
        public void AdditivePullPreservesTeamsInvalidationAndFrozenWalletChange()
        {
            var typed = new TypedQualificationCodec();
            var wallet = new ProjectionWalletRemoval(new ProjectionWalletKey("test.coin"), 3, "invalidation");
            V invalidation = Obj(("entityType", V.String("teams")), ("entityKey", Obj(("teamId", V.String(TeamsServiceTests.Team.ToString("D"))))),
                ("revision", V.String("9007199254740993")), ("kind", V.String("invalidation")));
            V page = Obj(("protocolVersion", V.Integer(1)), ("resetRequired", V.Boolean(false)), ("committedThrough", V.String("4")),
                ("changes", V.Array(new[] { Obj(("feedRevision", V.String("4")), ("changes", V.Array(new[] { invalidation, typed.ToDiagnostic<IProjectionChange>(wallet) }))) })),
                ("nextCursor", V.String("AQIDBAUGBwgJCgsM")), ("hasMore", V.Boolean(false)), ("serverTime", V.Integer(1_790_000_000_000)));
            var bytes = new QualificationMessagePackCodec().Encode("teams.schema.json#/$defs/pullResponse", page);
            var decoded = Assert.IsType<PullPage>(new TeamsSupportingWireCodec().Decode<IPullResponse>(bytes));
            Assert.Equal(9_007_199_254_740_993, Assert.IsType<TeamsProjectionInvalidation>(decoded.Changes[0].Changes[0]).Revision);
            Assert.IsType<ProjectionWalletRemoval>(decoded.Changes[0].Changes[1]);
            Assert.Throws<QualificationCodecException>(() => new MessagePackWireCodec().Decode<IPullResponse>(bytes));
        }

        private static V Obj(params (string Key, V Value)[] values) => V.Object(values.Select(p => new KeyValuePair<string, V>(p.Key, p.Value)));
    }
}
