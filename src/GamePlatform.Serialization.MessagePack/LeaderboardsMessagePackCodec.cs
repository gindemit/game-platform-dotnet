using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GamePlatform.Wire.Contracts.Leaderboards;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
using K = GamePlatform.Serialization.MessagePack.QualificationValueKind;
namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>Closed, bounded named-map codec for the separately pinned leaderboard schema.</summary>
    public sealed class LeaderboardsMessagePackCodec
    {
        private const string Schema = "leaderboards.schema.json#/$defs/";
        private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();
        public byte[] EncodeQuery(LeaderboardsQuery value)
        {
            var fields = new List<(string, V)> { ("boardId", V.String(value.BoardId)), ("scope", V.String(value.Scope)), ("limit", V.Integer(value.Limit)) };
            if (value.CountryCode != null)
                fields.Add(("countryCode", V.String(value.CountryCode)));
            if (value.Cursor != null)
                fields.Add(("cursor", V.String(value.Cursor)));
            return codec.Encode(Schema + "queryRequest", Obj(fields.ToArray()));
        }
        public LeaderboardsQuery DecodeQuery(byte[] bytes)
        {
            var p = codec.Decode(Schema + "queryRequest", bytes).Properties;
            return new LeaderboardsQuery(p["boardId"].StringValue, p["scope"].StringValue,
                p.TryGetValue("countryCode", out var country) ? country.StringValue : null,
                p.TryGetValue("cursor", out var cursor) ? cursor.StringValue : null, checked((int)p["limit"].IntegerValue));
        }
        public byte[] EncodeCountry(LeaderboardsCountryCommand value)
        {
            var fields = new List<(string, V)> { ("operationId", Id(value.OperationId)), ("boardId", V.String(value.BoardId)),
                ("participantKind", V.String(value.ParticipantKind)), ("countryCode", V.String(value.CountryCode)) };
            if (value.TeamId.HasValue)
                fields.Add(("teamId", Id(value.TeamId.Value)));
            return codec.Encode(Schema + "countryRequest", Obj(fields.ToArray()));
        }
        public LeaderboardsCountryCommand DecodeCountry(byte[] bytes)
        {
            var p = codec.Decode(Schema + "countryRequest", bytes).Properties;
            return new LeaderboardsCountryCommand(Guid.Parse(p["operationId"].StringValue), p["boardId"].StringValue,
                p["participantKind"].StringValue, p["countryCode"].StringValue,
                p.TryGetValue("teamId", out var team) ? Guid.Parse(team.StringValue) : (Guid?)null);
        }
        public byte[] EncodeCountryResponse(LeaderboardsCountryResponse value) => codec.Encode(Schema + "countryResponse", Obj(
            ("operationId", Id(value.OperationId)), ("boardId", V.String(value.BoardId)), ("participantKind", V.String(value.ParticipantKind)),
            ("participantId", Id(value.ParticipantId)), ("countryCode", V.String(value.CountryCode)), ("effectiveAt", Wide(value.EffectiveAt))));
        public LeaderboardsCountryResponse DecodeCountryResponse(byte[] bytes)
        {
            var p = codec.Decode(Schema + "countryResponse", bytes).Properties;
            return new LeaderboardsCountryResponse(Guid.Parse(p["operationId"].StringValue), p["boardId"].StringValue,
                p["participantKind"].StringValue, Guid.Parse(p["participantId"].StringValue), p["countryCode"].StringValue, Wide(p["effectiveAt"]));
        }
        public byte[] EncodeResponse(LeaderboardsResponse value) => codec.Encode(Schema + "queryResponse", Obj(
            ("boardId", V.String(value.BoardId)), ("participantKind", V.String(value.ParticipantKind)), ("scope", V.String(value.Scope)),
            ("countryCode", Text(value.CountryCode)), ("periodId", V.String(value.PeriodId)), ("periodStart", Wide(value.PeriodStart)),
            ("periodEnd", Wide(value.PeriodEnd)), ("resetAt", Wide(value.ResetAt)), ("snapshotId", Id(value.SnapshotId)),
            ("snapshotCreatedAt", Wide(value.SnapshotCreatedAt)), ("expiresAt", Wide(value.ExpiresAt)), ("serverTime", Wide(value.ServerTime)),
            ("scoreAuthority", V.String(value.ScoreAuthority)), ("entries", V.Array(value.Entries.Select(Entry).ToArray())),
            ("nextCursor", Text(value.NextCursor)), ("self", value.Self == null ? V.Null : Entry(value.Self)), ("snapshotLimit", V.Integer(value.SnapshotLimit)),
            ("countrySelection", value.CountrySelection == null ? V.Null : Obj(("currentCountryCode", Text(value.CountrySelection.CurrentCountryCode)),
                ("pendingCountryCode", Text(value.CountrySelection.PendingCountryCode)),
                ("pendingEffectiveAt", value.CountrySelection.PendingEffectiveAt.HasValue ? Wide(value.CountrySelection.PendingEffectiveAt.Value) : V.Null)))));
        public LeaderboardsResponse DecodeResponse(byte[] bytes)
        {
            var p = codec.Decode(Schema + "queryResponse", bytes).Properties;
            LeaderboardsCountrySelection? selection = null;
            if (p["countrySelection"].Kind != K.Null)
            {
                var c = p["countrySelection"].Properties;
                selection = new LeaderboardsCountrySelection(Text(c["currentCountryCode"]), Text(c["pendingCountryCode"]),
                    c["pendingEffectiveAt"].Kind == K.Null ? (long?)null : Wide(c["pendingEffectiveAt"]));
            }
            return new LeaderboardsResponse(p["boardId"].StringValue, p["participantKind"].StringValue, p["scope"].StringValue,
                Text(p["countryCode"]), p["periodId"].StringValue, Wide(p["periodStart"]), Wide(p["periodEnd"]), Wide(p["resetAt"]),
                Guid.Parse(p["snapshotId"].StringValue), Wide(p["snapshotCreatedAt"]), Wide(p["expiresAt"]), p["scoreAuthority"].StringValue,
                p["entries"].Items.Select(ReadEntry).ToArray(), Text(p["nextCursor"]), p["self"].Kind == K.Null ? null : ReadEntry(p["self"]),
                checked((int)p["snapshotLimit"].IntegerValue), Wide(p["serverTime"]), selection);
        }
        private static V Entry(LeaderboardEntry value) => Obj(("participantId", Id(value.ParticipantId)), ("rank", Wide(value.Rank)),
            ("score", Wide(value.Score)), ("displayName", V.String(value.DisplayName)), ("avatarKey", Text(value.AvatarKey)),
            ("badgeId", Text(value.BadgeId)), ("countryCode", Text(value.CountryCode)));
        private static LeaderboardEntry ReadEntry(V value)
        {
            var p = value.Properties;
            return new LeaderboardEntry(Guid.Parse(p["participantId"].StringValue), Wide(p["rank"]), Wide(p["score"]),
                p["displayName"].StringValue, Text(p["avatarKey"]), Text(p["badgeId"]), Text(p["countryCode"]));
        }
        private static V Obj(params (string, V)[] fields) => V.Object(fields.Select(x => new KeyValuePair<string, V>(x.Item1, x.Item2)).ToArray());
        private static V Id(Guid value) => V.String(value.ToString("D"));
        private static V Wide(long value) => V.String(value.ToString(CultureInfo.InvariantCulture));
        private static long Wide(V value) => long.Parse(value.StringValue, CultureInfo.InvariantCulture);
        private static V Text(string? value) => value == null ? V.Null : V.String(value);
        private static string? Text(V value) => value.Kind == K.Null ? null : value.StringValue;
    }
}
