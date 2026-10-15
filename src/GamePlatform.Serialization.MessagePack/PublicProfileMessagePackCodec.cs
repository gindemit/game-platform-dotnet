using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GamePlatform.Wire.Contracts.Profiles;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
using K = GamePlatform.Serialization.MessagePack.QualificationValueKind;

namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>
    /// Closed named-map codec for the public profile query schema.
    /// </summary>
    public sealed class PublicProfileMessagePackCodec
    {
        private const string Schema = "profiles.schema.json#/$defs/";
        private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();

        public byte[] EncodeQuery(Guid playerId) => codec.Encode(Schema + "publicQueryRequest", Obj(("protocolVersion", V.Integer(1)), ("playerId", Id(playerId))));

        public Guid DecodeQuery(byte[] bytes) => Guid.Parse(codec.Decode(Schema + "publicQueryRequest", bytes).Properties["playerId"].StringValue);

        public byte[] EncodeResponse(PublicProfileResponse value) => codec.Encode(Schema + "publicQueryResponse", Obj(
            ("protocolVersion", V.Integer(1)), ("appId", Id(value.AppId)), ("playerId", Id(value.PlayerId)), ("serverTime", V.Integer(value.ServerTime)),
            ("profile", value.Profile == null ? V.Null : Obj(("displayName", V.String(value.Profile.DisplayName)), ("avatarKey", Text(value.Profile.AvatarKey)),
                ("locale", Text(value.Profile.Locale)), ("revision", V.String(value.Profile.Revision.ToString(CultureInfo.InvariantCulture))))),
            ("team", value.Team == null ? V.Null : Obj(("teamId", Id(value.Team.TeamId)), ("name", V.String(value.Team.Name)), ("badgeId", V.String(value.Team.BadgeId))))));

        public PublicProfileResponse DecodeResponse(byte[] bytes)
        {
            var p = codec.Decode(Schema + "publicQueryResponse", bytes).Properties;
            PublicProfile? profile = null;
            if (p["profile"].Kind != K.Null)
            {
                var x = p["profile"].Properties;
                profile = new PublicProfile(x["displayName"].StringValue, Text(x["avatarKey"]), Text(x["locale"]), long.Parse(x["revision"].StringValue, CultureInfo.InvariantCulture));
            }
            PublicProfileTeam? team = null;
            if (p["team"].Kind != K.Null)
            {
                var x = p["team"].Properties;
                team = new PublicProfileTeam(Guid.Parse(x["teamId"].StringValue), x["name"].StringValue, x["badgeId"].StringValue);
            }
            return new PublicProfileResponse(Guid.Parse(p["appId"].StringValue), Guid.Parse(p["playerId"].StringValue), p["serverTime"].IntegerValue, profile, team);
        }

        private static V Obj(params (string, V)[] fields) => V.Object(fields.Select(x => new KeyValuePair<string, V>(x.Item1, x.Item2)).ToArray());
        private static V Id(Guid value) => V.String(value.ToString("D"));
        private static V Text(string? value) => value == null ? V.Null : V.String(value);
        private static string? Text(V value) => value.Kind == K.Null ? null : value.StringValue;
    }
}
