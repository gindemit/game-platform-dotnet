#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GamePlatform.Wire.Contracts.Teams;
using V = GamePlatform.Serialization.MessagePack.QualificationValue;
using K = GamePlatform.Serialization.MessagePack.QualificationValueKind;
namespace GamePlatform.Serialization.MessagePack
{
    public sealed class TeamsMessagePackCodec
    {
        private const string Schema = "teams.schema.json#/$defs/";
        private readonly QualificationMessagePackCodec codec = new QualificationMessagePackCodec();
        public byte[] EncodeResponse(TeamsResponse value) => codec.Encode(Schema + "response", Write(value));
        public TeamsResponse DecodeResponse(byte[] bytes) => ReadTeamsResponse(codec.Decode(Schema + "response", bytes));
        public byte[] EncodeQuery(TeamsQuery value) => codec.Encode(Schema + "queryRequest", WriteQuery(value));
        public byte[] EncodeCommand(TeamsCommand value) => codec.Encode(Schema + "commandRequest", WriteCommand(value));
        public TeamsCommand DecodeCommand(byte[] bytes)
        {
            var p = codec.Decode(Schema + "commandRequest", bytes).Properties;
            var q = p["payload"].Properties;
            string? Text(string key) => q.TryGetValue(key, out var value) ? value.StringValue : null;
            Guid? Id(string key) => q.TryGetValue(key, out var value) ? Guid.Parse(value.StringValue) : (Guid?)null;
            int? Count(string key) => q.TryGetValue(key, out var value) ? checked((int)value.IntegerValue) : (int?)null;
            return new TeamsCommand(Guid.Parse(p["operationId"].StringValue), p["action"].StringValue,
                p.TryGetValue("teamId", out var team) ? Guid.Parse(team.StringValue) : (Guid?)null,
                p.TryGetValue("expectedRevision", out var revision) ? long.Parse(revision.StringValue, CultureInfo.InvariantCulture) : (long?)null,
                new TeamsCommandPayload(Text("name"), Text("description"), Text("badgeId"), Text("language"), Text("joinPolicy"), Count("requiredLevel"), Count("capacity"),
                    Id("targetPlayerId"), Id("inviteId"), Id("messageId"), Id("requestId"), Text("role"), Text("text"), Text("reason"), Count("durationMinutes")));
        }
        private static V Write(TeamSummary value) => Obj(("teamId", Uuid(value.TeamId)), ("name", V.String(value.Name)), ("description", V.String(value.Description)), ("badgeId", V.String(value.BadgeId)), ("language", V.String(value.Language)), ("joinPolicy", V.String(value.JoinPolicy)), ("requiredLevel", V.Integer(value.RequiredLevel)), ("capacity", V.Integer(value.Capacity)), ("memberCount", V.Integer(value.MemberCount)), ("revision", Wide(value.Revision)), ("createdAtMilliseconds", Wide(value.CreatedAtMilliseconds)));
        private static TeamSummary ReadTeamSummary(V value)
        {
            var p = value.Properties;
            return new TeamSummary(Guid.Parse(p["teamId"].StringValue), p["name"].StringValue, p["description"].StringValue, p["badgeId"].StringValue, p["language"].StringValue, p["joinPolicy"].StringValue, checked((int)p["requiredLevel"].IntegerValue), checked((int)p["capacity"].IntegerValue), checked((int)p["memberCount"].IntegerValue), long.Parse(p["revision"].StringValue, CultureInfo.InvariantCulture), long.Parse(p["createdAtMilliseconds"].StringValue, CultureInfo.InvariantCulture));
        }
        private static V Write(TeamMembership value) => Obj(("teamId", Uuid(value.TeamId)), ("playerId", Uuid(value.PlayerId)), ("role", V.String(value.Role)), ("joinedAtMilliseconds", Wide(value.JoinedAtMilliseconds)), ("helpCount", Wide(value.HelpCount)));
        private static TeamMembership ReadTeamMembership(V value)
        {
            var p = value.Properties;
            return new TeamMembership(Guid.Parse(p["teamId"].StringValue), Guid.Parse(p["playerId"].StringValue), p["role"].StringValue, long.Parse(p["joinedAtMilliseconds"].StringValue, CultureInfo.InvariantCulture), long.Parse(p["helpCount"].StringValue, CultureInfo.InvariantCulture));
        }
        private static V Write(TeamMember value) => Obj(("teamId", Uuid(value.TeamId)), ("playerId", Uuid(value.PlayerId)), ("role", V.String(value.Role)), ("joinedAtMilliseconds", Wide(value.JoinedAtMilliseconds)), ("helpCount", Wide(value.HelpCount)), ("displayName", V.String(value.DisplayName)), ("avatarKey", value.AvatarKey == null ? V.Null : V.String(value.AvatarKey)), ("mutedUntilMilliseconds", Wide(value.MutedUntilMilliseconds)));
        private static TeamMember ReadTeamMember(V value)
        {
            var p = value.Properties;
            return new TeamMember(Guid.Parse(p["teamId"].StringValue), Guid.Parse(p["playerId"].StringValue), p["role"].StringValue, long.Parse(p["joinedAtMilliseconds"].StringValue, CultureInfo.InvariantCulture), long.Parse(p["helpCount"].StringValue, CultureInfo.InvariantCulture), p["displayName"].StringValue, p["avatarKey"].Kind == K.Null ? null : p["avatarKey"].StringValue, long.Parse(p["mutedUntilMilliseconds"].StringValue, CultureInfo.InvariantCulture));
        }
        private static V Write(TeamMessage value) => Obj(("messageId", Uuid(value.MessageId)), ("playerId", Uuid(value.PlayerId)), ("displayName", V.String(value.DisplayName)), ("text", V.String(value.Text)), ("kind", V.String(value.Kind)), ("createdAtMilliseconds", Wide(value.CreatedAtMilliseconds)), ("deleted", V.Boolean(value.Deleted)));
        private static TeamMessage ReadTeamMessage(V value)
        {
            var p = value.Properties;
            return new TeamMessage(Guid.Parse(p["messageId"].StringValue), Guid.Parse(p["playerId"].StringValue), p["displayName"].StringValue, p["text"].StringValue, p["kind"].StringValue, long.Parse(p["createdAtMilliseconds"].StringValue, CultureInfo.InvariantCulture), p["deleted"].BooleanValue);
        }
        private static V Write(TeamJoinRequest value) => Obj(("requestId", Uuid(value.RequestId)), ("teamId", Uuid(value.TeamId)), ("playerId", Uuid(value.PlayerId)), ("displayName", V.String(value.DisplayName)), ("status", V.String(value.Status)), ("createdAtMilliseconds", Wide(value.CreatedAtMilliseconds)));
        private static TeamJoinRequest ReadTeamJoinRequest(V value)
        {
            var p = value.Properties;
            return new TeamJoinRequest(Guid.Parse(p["requestId"].StringValue), Guid.Parse(p["teamId"].StringValue), Guid.Parse(p["playerId"].StringValue), p["displayName"].StringValue, p["status"].StringValue, long.Parse(p["createdAtMilliseconds"].StringValue, CultureInfo.InvariantCulture));
        }
        private static V Write(TeamInvitation value) => Obj(("inviteId", Uuid(value.InviteId)), ("teamId", Uuid(value.TeamId)), ("playerId", Uuid(value.PlayerId)), ("inviterPlayerId", Uuid(value.InviterPlayerId)), ("status", V.String(value.Status)), ("createdAtMilliseconds", Wide(value.CreatedAtMilliseconds)), ("expiresAtMilliseconds", Wide(value.ExpiresAtMilliseconds)));
        private static TeamInvitation ReadTeamInvitation(V value)
        {
            var p = value.Properties;
            return new TeamInvitation(Guid.Parse(p["inviteId"].StringValue), Guid.Parse(p["teamId"].StringValue), Guid.Parse(p["playerId"].StringValue), Guid.Parse(p["inviterPlayerId"].StringValue), p["status"].StringValue, long.Parse(p["createdAtMilliseconds"].StringValue, CultureInfo.InvariantCulture), long.Parse(p["expiresAtMilliseconds"].StringValue, CultureInfo.InvariantCulture));
        }
        private static V Write(TeamHelpRequest value) => Obj(("requestId", Uuid(value.RequestId)), ("teamId", Uuid(value.TeamId)), ("playerId", Uuid(value.PlayerId)), ("displayName", V.String(value.DisplayName)), ("currentCount", V.Integer(value.CurrentCount)), ("requiredCount", V.Integer(value.RequiredCount)), ("createdAtMilliseconds", Wide(value.CreatedAtMilliseconds)), ("expiresAtMilliseconds", Wide(value.ExpiresAtMilliseconds)), ("contributedByMe", V.Boolean(value.ContributedByMe)), ("status", V.String(value.Status)));
        private static TeamHelpRequest ReadTeamHelpRequest(V value)
        {
            var p = value.Properties;
            return new TeamHelpRequest(Guid.Parse(p["requestId"].StringValue), Guid.Parse(p["teamId"].StringValue), Guid.Parse(p["playerId"].StringValue), p["displayName"].StringValue, checked((int)p["currentCount"].IntegerValue), checked((int)p["requiredCount"].IntegerValue), long.Parse(p["createdAtMilliseconds"].StringValue, CultureInfo.InvariantCulture), long.Parse(p["expiresAtMilliseconds"].StringValue, CultureInfo.InvariantCulture), p["contributedByMe"].BooleanValue, p["status"].StringValue);
        }
        private static V Write(TeamsResponse value) => Obj(("protocolVersion", V.Integer(1)), ("appId", Uuid(value.AppId)), ("accountId", Uuid(value.AccountId)), ("playerId", Uuid(value.PlayerId)), ("serverTimeMilliseconds", Wide(value.ServerTimeMilliseconds)), ("operationId", value.OperationId.HasValue ? Uuid(value.OperationId.Value) : V.Null), ("revision", Wide(value.Revision)), ("team", value.Team == null ? V.Null : Write(value.Team)), ("membership", value.Membership == null ? V.Null : Write(value.Membership)), ("teams", V.Array(value.Teams.Select(Write))), ("members", V.Array(value.Members.Select(Write))), ("messages", V.Array(value.Messages.Select(Write))), ("requests", V.Array(value.Requests.Select(Write))), ("invites", V.Array(value.Invites.Select(Write))), ("helpRequests", V.Array(value.HelpRequests.Select(Write))), ("nextCursor", value.NextCursor == null ? V.Null : V.String(value.NextCursor)), ("resultCode", value.ResultCode == null ? V.Null : V.String(value.ResultCode)));
        private static TeamsResponse ReadTeamsResponse(V value)
        {
            var p = value.Properties;
            return new TeamsResponse(Guid.Parse(p["appId"].StringValue), Guid.Parse(p["accountId"].StringValue), Guid.Parse(p["playerId"].StringValue), long.Parse(p["serverTimeMilliseconds"].StringValue, CultureInfo.InvariantCulture), p["operationId"].Kind == K.Null ? (Guid?)null : Guid.Parse(p["operationId"].StringValue), long.Parse(p["revision"].StringValue, CultureInfo.InvariantCulture), p["team"].Kind == K.Null ? null : ReadTeamSummary(p["team"]), p["membership"].Kind == K.Null ? null : ReadTeamMembership(p["membership"]), p["teams"].Items.Select(ReadTeamSummary).ToArray(), p["members"].Items.Select(ReadTeamMember).ToArray(), p["messages"].Items.Select(ReadTeamMessage).ToArray(), p["requests"].Items.Select(ReadTeamJoinRequest).ToArray(), p["invites"].Items.Select(ReadTeamInvitation).ToArray(), p["helpRequests"].Items.Select(ReadTeamHelpRequest).ToArray(), p["nextCursor"].Kind == K.Null ? null : p["nextCursor"].StringValue, p["resultCode"].Kind == K.Null ? null : p["resultCode"].StringValue);
        }
        private static V WriteQuery(TeamsQuery value)
        {
            var fields = new List<(string, V)> { ("protocolVersion", V.Integer(1)), ("view", V.String(value.View)), ("pageSize", V.Integer(value.PageSize)) };
            if (value.TeamId.HasValue) fields.Add(("teamId", Uuid(value.TeamId.Value)));
            if (value.Search != null) fields.Add(("search", V.String(value.Search)));
            if (value.Cursor != null) fields.Add(("cursor", V.String(value.Cursor)));
            return Obj(fields.ToArray());
        }
        private static V WriteCommand(TeamsCommand value)
        {
            var fields = new List<(string, V)> { ("protocolVersion", V.Integer(1)), ("operationId", Uuid(value.OperationId)), ("action", V.String(value.Action)), ("payload", WritePayload(value.Payload)) };
            if (value.TeamId.HasValue) fields.Add(("teamId", Uuid(value.TeamId.Value)));
            if (value.ExpectedRevision.HasValue) fields.Add(("expectedRevision", Wide(value.ExpectedRevision.Value)));
            return Obj(fields.ToArray());
        }
        private static V WritePayload(TeamsCommandPayload value)
        {
            var fields = new List<(string, V)>();
            if (value.Name != null) fields.Add(("name", V.String(value.Name)));
            if (value.Description != null) fields.Add(("description", V.String(value.Description)));
            if (value.BadgeId != null) fields.Add(("badgeId", V.String(value.BadgeId)));
            if (value.Language != null) fields.Add(("language", V.String(value.Language)));
            if (value.JoinPolicy != null) fields.Add(("joinPolicy", V.String(value.JoinPolicy)));
            if (value.RequiredLevel.HasValue) fields.Add(("requiredLevel", V.Integer(value.RequiredLevel.Value)));
            if (value.Capacity.HasValue) fields.Add(("capacity", V.Integer(value.Capacity.Value)));
            if (value.TargetPlayerId.HasValue) fields.Add(("targetPlayerId", Uuid(value.TargetPlayerId.Value)));
            if (value.InviteId.HasValue) fields.Add(("inviteId", Uuid(value.InviteId.Value)));
            if (value.MessageId.HasValue) fields.Add(("messageId", Uuid(value.MessageId.Value)));
            if (value.RequestId.HasValue) fields.Add(("requestId", Uuid(value.RequestId.Value)));
            if (value.Role != null) fields.Add(("role", V.String(value.Role)));
            if (value.Text != null) fields.Add(("text", V.String(value.Text)));
            if (value.Reason != null) fields.Add(("reason", V.String(value.Reason)));
            if (value.DurationMinutes.HasValue) fields.Add(("durationMinutes", V.Integer(value.DurationMinutes.Value)));
            return Obj(fields.ToArray());
        }
        private static V Uuid(Guid value) => V.String(value.ToString("D"));
        private static V Wide(long value) => V.String(value.ToString(CultureInfo.InvariantCulture));
        private static V Obj(params (string Key, V Value)[] values) => V.Object(values.Select(p => new KeyValuePair<string, V>(p.Key, p.Value)));
    }
}
