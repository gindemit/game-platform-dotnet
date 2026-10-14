using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GamePlatform.Wire.Contracts.Friends;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
using K = GamePlatform.Serialization.MessagePack.QualificationValueKind;
namespace GamePlatform.Serialization.MessagePack
{
    public sealed class FriendsMessagePackCodec
    {
        private const string Schema = "friends.schema.json#/$defs/";
        private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();
        public byte[] EncodeResponse(FriendsResponse value) => codec.Encode(Schema + "response", Write(value));
        public FriendsResponse DecodeResponse(byte[] bytes) => ReadFriendsResponse(codec.Decode(Schema + "response", bytes));
        public byte[] EncodeQuery(FriendsQuery value)
        {
            var fields = new List<(string, V)> { ("protocolVersion", V.Integer(1)), ("view", V.String(value.View)), ("pageSize", V.Integer(value.PageSize)) };
            if (value.PublicCode != null)
                fields.Add(("publicCode", V.String(value.PublicCode)));
            if (value.Cursor != null)
                fields.Add(("cursor", V.String(value.Cursor)));
            return codec.Encode(Schema + "queryRequest", Obj(fields.ToArray()));
        }
        public FriendsQuery DecodeQuery(byte[] bytes)
        {
            var p = codec.Decode(Schema + "queryRequest", bytes).Properties;
            return new FriendsQuery(p["view"].StringValue, p.TryGetValue("publicCode", out var code) ? code.StringValue : null, p.TryGetValue("cursor", out var cursor) ? cursor.StringValue : null, checked((int)p["pageSize"].IntegerValue));
        }
        public byte[] EncodeCommand(FriendsCommand value)
        {
            var p = value.Payload;
            var fields = new List<(string, V)>();
            if (p.PublicCode != null)
                fields.Add(("publicCode", V.String(p.PublicCode)));
            if (p.TargetPlayerId.HasValue)
                fields.Add(("targetPlayerId", Id(p.TargetPlayerId.Value)));
            if (p.RequestId.HasValue)
                fields.Add(("requestId", Id(p.RequestId.Value)));
            if (p.AllowRequests.HasValue)
                fields.Add(("allowRequests", V.Boolean(p.AllowRequests.Value)));
            return codec.Encode(Schema + "commandRequest", Obj(("protocolVersion", V.Integer(1)), ("operationId", Id(value.OperationId)), ("action", V.String(value.Action)), ("payload", Obj(fields.ToArray()))));
        }
        public FriendsCommand DecodeCommand(byte[] bytes)
        {
            var p = codec.Decode(Schema + "commandRequest", bytes).Properties;
            var q = p["payload"].Properties;
            return new FriendsCommand(Guid.Parse(p["operationId"].StringValue), p["action"].StringValue, new FriendsCommandPayload(q.TryGetValue("publicCode", out var code) ? code.StringValue : null, q.TryGetValue("targetPlayerId", out var target) ? Guid.Parse(target.StringValue) : (Guid?)null, q.TryGetValue("requestId", out var request) ? Guid.Parse(request.StringValue) : (Guid?)null, q.TryGetValue("allowRequests", out var allow) ? allow.BooleanValue : (bool?)null));
        }
        private static V Write(PublicPlayer value) => Obj(("playerId", Id(value.PlayerId)), ("displayName", V.String(value.DisplayName)), ("avatarKey", Text(value.AvatarKey)));
        private static PublicPlayer ReadPublicPlayer(V value)
        {
            var p = value.Properties;
            return new PublicPlayer(Guid.Parse(p["playerId"].StringValue), p["displayName"].StringValue, Text(p["avatarKey"]));
        }
        private static V Write(Friend value) => Obj(("player", Write(value.Player)), ("sinceMilliseconds", Wide(value.SinceMilliseconds)));
        private static Friend ReadFriend(V value)
        {
            var p = value.Properties;
            return new Friend(ReadPublicPlayer(p["player"]), Wide(p["sinceMilliseconds"]));
        }
        private static V Write(BlockedPlayer value) => Obj(("player", Write(value.Player)), ("blockedAtMilliseconds", Wide(value.BlockedAtMilliseconds)));
        private static BlockedPlayer ReadBlockedPlayer(V value)
        {
            var p = value.Properties;
            return new BlockedPlayer(ReadPublicPlayer(p["player"]), Wide(p["blockedAtMilliseconds"]));
        }
        private static V Write(FriendRequest value) => Obj(("requestId", Id(value.RequestId)), ("senderPlayerId", Id(value.SenderPlayerId)), ("recipientPlayerId", Id(value.RecipientPlayerId)), ("status", V.String(value.Status)), ("createdAtMilliseconds", Wide(value.CreatedAtMilliseconds)), ("expiresAtMilliseconds", Wide(value.ExpiresAtMilliseconds)), ("peer", Write(value.Peer)));
        private static FriendRequest ReadFriendRequest(V value)
        {
            var p = value.Properties;
            return new FriendRequest(Guid.Parse(p["requestId"].StringValue), Guid.Parse(p["senderPlayerId"].StringValue), Guid.Parse(p["recipientPlayerId"].StringValue), p["status"].StringValue, Wide(p["createdAtMilliseconds"]), Wide(p["expiresAtMilliseconds"]), ReadPublicPlayer(p["peer"]));
        }
        private static V Write(FriendsResponse value) => Obj(("protocolVersion", V.Integer(1)), ("appId", Id(value.AppId)), ("accountId", Id(value.AccountId)), ("playerId", Id(value.PlayerId)), ("serverTimeMilliseconds", Wide(value.ServerTimeMilliseconds)), ("cacheExpiresAtMilliseconds", Wide(value.CacheExpiresAtMilliseconds)), ("operationId", value.OperationId.HasValue ? Id(value.OperationId.Value) : V.Null), ("view", Text(value.View)), ("revision", Wide(value.Revision)), ("graphRevision", Wide(value.GraphRevision)), ("myPublicCode", V.String(value.MyPublicCode)), ("allowRequests", V.Boolean(value.AllowRequests)), ("friendCount", V.Integer(value.FriendCount)), ("incomingCount", V.Integer(value.IncomingCount)), ("outgoingCount", V.Integer(value.OutgoingCount)), ("friends", V.Array(value.Friends.Select(Write))), ("requests", V.Array(value.Requests.Select(Write))), ("blockedPlayers", V.Array(value.BlockedPlayers.Select(Write))), ("suggestions", V.Array(value.Suggestions.Select(Write))), ("player", value.Player == null ? V.Null : Write(value.Player)), ("request", value.Request == null ? V.Null : Write(value.Request)), ("relationship", Text(value.Relationship)), ("relationshipRequestId", value.RelationshipRequestId.HasValue ? Id(value.RelationshipRequestId.Value) : V.Null), ("nextCursor", Text(value.NextCursor)), ("resultCode", Text(value.ResultCode)));
        private static FriendsResponse ReadFriendsResponse(V value)
        {
            var p = value.Properties;
            return new FriendsResponse(Guid.Parse(p["appId"].StringValue), Guid.Parse(p["accountId"].StringValue), Guid.Parse(p["playerId"].StringValue), Wide(p["serverTimeMilliseconds"]), Wide(p["cacheExpiresAtMilliseconds"]), p["operationId"].Kind == K.Null ? (Guid?)null : Guid.Parse(p["operationId"].StringValue), Text(p["view"]), Wide(p["revision"]), Wide(p["graphRevision"]), p["myPublicCode"].StringValue, p["allowRequests"].BooleanValue, checked((int)p["friendCount"].IntegerValue), checked((int)p["incomingCount"].IntegerValue), checked((int)p["outgoingCount"].IntegerValue), p["friends"].Items.Select(ReadFriend).ToArray(), p["requests"].Items.Select(ReadFriendRequest).ToArray(), p["blockedPlayers"].Items.Select(ReadBlockedPlayer).ToArray(), p["suggestions"].Items.Select(ReadPublicPlayer).ToArray(), p["player"].Kind == K.Null ? null : ReadPublicPlayer(p["player"]), p["request"].Kind == K.Null ? null : ReadFriendRequest(p["request"]), Text(p["relationship"]), p["relationshipRequestId"].Kind == K.Null ? (Guid?)null : Guid.Parse(p["relationshipRequestId"].StringValue), Text(p["nextCursor"]), Text(p["resultCode"]));
        }

        public byte[] EncodeRankingQuery(string boardId, int limit = 30, string? cursor = null)
        {
            if (boardId != "players-monthly" && boardId != "players-daily" && boardId != "players-weekly" || limit < 1 || limit > 50)
                throw new ArgumentException("Invalid friends ranking query.");
            var fields = new List<(string, V)> { ("protocolVersion", V.Integer(1)), ("boardId", V.String(boardId)), ("limit", V.Integer(limit)) };
            if (cursor != null)
                fields.Add(("cursor", V.String(cursor)));
            return codec.Encode(Schema + "rankingQueryRequest", Obj(fields.ToArray()));
        }
        public byte[] EncodeRankingResponse(FriendsRankingResponse value) => codec.Encode(Schema + "rankingQueryResponse", Obj(
         ("protocolVersion", V.Integer(1)), ("appId", Id(value.AppId!.Value)), ("accountId", Id(value.AccountId!.Value)), ("playerId", Id(value.PlayerId!.Value)),
         ("boardId", V.String(value.BoardId)), ("participantKind", V.String("player")), ("scope", V.String("friends")), ("countryCode", V.Null),
         ("periodId", V.String(value.PeriodId)), ("periodStart", Wide(value.PeriodStart)), ("periodEnd", Wide(value.PeriodEnd)), ("resetAt", Wide(value.ResetAt)),
         ("snapshotId", Id(value.SnapshotId)), ("snapshotCreatedAt", Wide(value.SnapshotCreatedAt)), ("expiresAt", Wide(value.ExpiresAt)), ("serverTime", Wide(value.ServerTime)),
         ("scoreAuthority", V.String(value.ScoreAuthority)), ("graphRevision", Wide(value.GraphRevision!.Value)), ("snapshotLimit", V.Integer(value.SnapshotLimit)),
         ("cohortSize", V.Integer(value.CohortSize!.Value)), ("entries", V.Array(value.Entries.Select(RankingEntry))), ("nextCursor", Text(value.NextCursor)),
         ("self", RankingEntry(value.Self!)), ("countrySelection", V.Null)));
        public FriendsRankingResponse DecodeRankingResponse(byte[] bytes)
        {
            var p = codec.Decode(Schema + "rankingQueryResponse", bytes).Properties;
            return new FriendsRankingResponse(p["boardId"].StringValue, p["participantKind"].StringValue, p["scope"].StringValue, Text(p["countryCode"]), p["periodId"].StringValue,
            Wide(p["periodStart"]), Wide(p["periodEnd"]), Wide(p["resetAt"]), Guid.Parse(p["snapshotId"].StringValue), Wide(p["snapshotCreatedAt"]), Wide(p["expiresAt"]),
            p["scoreAuthority"].StringValue, p["entries"].Items.Select(ReadRankingEntry).ToArray(), Text(p["nextCursor"]), ReadRankingEntry(p["self"]),
            checked((int)p["snapshotLimit"].IntegerValue), Wide(p["serverTime"]), null, Guid.Parse(p["appId"].StringValue), Guid.Parse(p["accountId"].StringValue), Guid.Parse(p["playerId"].StringValue),
            Wide(p["graphRevision"]), checked((int)p["cohortSize"].IntegerValue));
        }
        private static V RankingEntry(FriendsRankingEntry value) => Obj(("participantId", Id(value.ParticipantId)), ("rank", Wide(value.Rank)), ("score", Wide(value.Score)), ("displayName", V.String(value.DisplayName)), ("avatarKey", Text(value.AvatarKey)), ("badgeId", V.Null), ("countryCode", Text(value.CountryCode)));
        private static FriendsRankingEntry ReadRankingEntry(V value)
        {
            var p = value.Properties;
            return new FriendsRankingEntry(Guid.Parse(p["participantId"].StringValue), Wide(p["rank"]), Wide(p["score"]), p["displayName"].StringValue, Text(p["avatarKey"]), Text(p["badgeId"]), Text(p["countryCode"]));
        }
        private static V Obj(params (string, V)[] fields) => V.Object(fields.Select(x => new KeyValuePair<string, V>(x.Item1, x.Item2)));
        private static V Id(Guid value) => V.String(value.ToString("D"));
        private static V Wide(long value) => V.String(value.ToString(CultureInfo.InvariantCulture));
        private static long Wide(V value) => long.Parse(value.StringValue, CultureInfo.InvariantCulture);
        private static V Text(string? value) => value == null ? V.Null : V.String(value);
        private static string? Text(V value) => value.Kind == K.Null ? null : value.StringValue;
    }
}

