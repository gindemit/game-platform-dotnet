using System.Linq;
using GamePlatform.Core;
using D = GamePlatform.Features.Friends;
using W = GamePlatform.Wire.Contracts.Friends;
namespace GamePlatform.Transport.Http
{
    internal static class FriendsModelMapper
    {
        internal static D.PublicPlayer ToD(W.PublicPlayer value) => new D.PublicPlayer(value.PlayerId, value.DisplayName, value.AvatarKey);
        internal static D.Friend ToD(W.Friend value) => new D.Friend(ToD(value.Player), value.SinceMilliseconds);
        internal static D.BlockedPlayer ToD(W.BlockedPlayer value) => new D.BlockedPlayer(ToD(value.Player), value.BlockedAtMilliseconds);
        internal static D.FriendRequest ToD(W.FriendRequest value) => new D.FriendRequest(value.RequestId, value.SenderPlayerId, value.RecipientPlayerId, value.Status, value.CreatedAtMilliseconds, value.ExpiresAtMilliseconds, ToD(value.Peer));
        internal static D.FriendsResponse ToD(W.FriendsResponse value) => new D.FriendsResponse(value.AppId, value.AccountId, value.PlayerId, value.ServerTimeMilliseconds, value.CacheExpiresAtMilliseconds, value.OperationId, value.View, value.Revision, value.GraphRevision, value.MyPublicCode, value.AllowRequests, value.FriendCount, value.IncomingCount, value.OutgoingCount, value.Friends.Select(ToD).ToArray(), value.Requests.Select(ToD).ToArray(), value.BlockedPlayers.Select(ToD).ToArray(), value.Suggestions.Select(ToD).ToArray(), value.Player == null ? null : ToD(value.Player), value.Request == null ? null : ToD(value.Request), value.Relationship, value.RelationshipRequestId, value.NextCursor, value.ResultCode);
        internal static W.PublicPlayer ToW(D.PublicPlayer value) => new W.PublicPlayer(value.PlayerId, value.DisplayName, value.AvatarKey);
        internal static W.Friend ToW(D.Friend value) => new W.Friend(ToW(value.Player), value.SinceMilliseconds);
        internal static W.BlockedPlayer ToW(D.BlockedPlayer value) => new W.BlockedPlayer(ToW(value.Player), value.BlockedAtMilliseconds);
        internal static W.FriendRequest ToW(D.FriendRequest value) => new W.FriendRequest(value.RequestId, value.SenderPlayerId, value.RecipientPlayerId, value.Status, value.CreatedAtMilliseconds, value.ExpiresAtMilliseconds, ToW(value.Peer));
        internal static W.FriendsResponse ToW(D.FriendsResponse value) => new W.FriendsResponse(value.AppId, value.AccountId, value.PlayerId, value.ServerTimeMilliseconds, value.CacheExpiresAtMilliseconds, value.OperationId, value.View, value.Revision, value.GraphRevision, value.MyPublicCode, value.AllowRequests, value.FriendCount, value.IncomingCount, value.OutgoingCount, value.Friends.Select(ToW).ToArray(), value.Requests.Select(ToW).ToArray(), value.BlockedPlayers.Select(ToW).ToArray(), value.Suggestions.Select(ToW).ToArray(), value.Player == null ? null : ToW(value.Player), value.Request == null ? null : ToW(value.Request), value.Relationship, value.RelationshipRequestId, value.NextCursor, value.ResultCode);

        internal static W.FriendsCommand ToW(D.FriendsCommand value) => new W.FriendsCommand(value.OperationId.Value, value.Action, new W.FriendsCommandPayload(value.Payload.PublicCode, value.Payload.TargetPlayerId, value.Payload.RequestId, value.Payload.AllowRequests));
        internal static D.FriendsCommand ToD(W.FriendsCommand value) => new D.FriendsCommand(new OperationId(value.OperationId), value.Action, new D.FriendsCommandPayload(value.Payload.PublicCode, value.Payload.TargetPlayerId, value.Payload.RequestId, value.Payload.AllowRequests));
    }
}

