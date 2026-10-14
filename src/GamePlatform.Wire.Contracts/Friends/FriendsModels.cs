#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
namespace GamePlatform.Wire.Contracts.Friends
{
    public sealed class PublicPlayer
    {
        public PublicPlayer(Guid playerId, string displayName, string? avatarKey)
        {
            FriendsValidation.Id(playerId);
            FriendsValidation.Text(displayName, 64);
            if (avatarKey != null)
                FriendsValidation.Text(avatarKey, 128, true);
            PlayerId = playerId;
            DisplayName = displayName;
            AvatarKey = avatarKey;
        }
        public Guid PlayerId
        {
            get;
        }
        public string DisplayName
        {
            get;
        }
        public string? AvatarKey
        {
            get;
        }
    }
    public sealed class Friend
    {
        public Friend(PublicPlayer player, long sinceMilliseconds)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            FriendsValidation.Time(sinceMilliseconds);
            Player = player;
            SinceMilliseconds = sinceMilliseconds;
        }
        public PublicPlayer Player
        {
            get;
        }
        public long SinceMilliseconds
        {
            get;
        }
    }
    public sealed class BlockedPlayer
    {
        public BlockedPlayer(PublicPlayer player, long blockedAtMilliseconds)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            FriendsValidation.Time(blockedAtMilliseconds);
            Player = player;
            BlockedAtMilliseconds = blockedAtMilliseconds;
        }
        public PublicPlayer Player
        {
            get;
        }
        public long BlockedAtMilliseconds
        {
            get;
        }
    }
    public sealed class FriendRequest
    {
        public FriendRequest(Guid requestId, Guid senderPlayerId, Guid recipientPlayerId, string status, long createdAtMilliseconds, long expiresAtMilliseconds, PublicPlayer peer)
        {
            FriendsValidation.Operation(requestId);
            FriendsValidation.Id(senderPlayerId);
            FriendsValidation.Id(recipientPlayerId);
            FriendsValidation.Time(createdAtMilliseconds);
            FriendsValidation.Time(expiresAtMilliseconds);
            if (senderPlayerId == recipientPlayerId || expiresAtMilliseconds <= createdAtMilliseconds || peer == null || !new[] { "pending", "accepted", "declined", "cancelled", "expired" }.Contains(status) || peer.PlayerId != senderPlayerId && peer.PlayerId != recipientPlayerId)
                throw new ArgumentException("Invalid friend request.");
            RequestId = requestId;
            SenderPlayerId = senderPlayerId;
            RecipientPlayerId = recipientPlayerId;
            Status = status;
            CreatedAtMilliseconds = createdAtMilliseconds;
            ExpiresAtMilliseconds = expiresAtMilliseconds;
            Peer = peer;
        }
        public Guid RequestId
        {
            get;
        }
        public Guid SenderPlayerId
        {
            get;
        }
        public Guid RecipientPlayerId
        {
            get;
        }
        public string Status
        {
            get;
        }
        public long CreatedAtMilliseconds
        {
            get;
        }
        public long ExpiresAtMilliseconds
        {
            get;
        }
        public PublicPlayer Peer
        {
            get;
        }
    }
    public sealed class FriendsResponse
    {
        public FriendsResponse(Guid appId, Guid accountId, Guid playerId, long serverTimeMilliseconds, long cacheExpiresAtMilliseconds, Guid? operationId, string? view, long revision, long graphRevision, string myPublicCode, bool allowRequests, int friendCount, int incomingCount, int outgoingCount, IReadOnlyList<Friend> friends, IReadOnlyList<FriendRequest> requests, IReadOnlyList<BlockedPlayer> blockedPlayers, IReadOnlyList<PublicPlayer> suggestions, PublicPlayer? player, FriendRequest? request, string? relationship, Guid? relationshipRequestId, string? nextCursor, string? resultCode)
        {
            if (friends == null || friends.Count > 50 || friends.Any(x => x == null))
                throw new ArgumentException("Invalid bounded list.", nameof(friends));
            if (requests == null || requests.Count > 50 || requests.Any(x => x == null))
                throw new ArgumentException("Invalid bounded list.", nameof(requests));
            if (blockedPlayers == null || blockedPlayers.Count > 50 || blockedPlayers.Any(x => x == null))
                throw new ArgumentException("Invalid bounded list.", nameof(blockedPlayers));
            if (suggestions == null || suggestions.Count > 50 || suggestions.Any(x => x == null))
                throw new ArgumentException("Invalid bounded list.", nameof(suggestions));
            FriendsValidation.Id(appId);
            FriendsValidation.Id(accountId);
            FriendsValidation.Id(playerId);
            FriendsValidation.Time(serverTimeMilliseconds);
            FriendsValidation.Time(cacheExpiresAtMilliseconds);
            FriendsValidation.Code(myPublicCode);
            FriendsValidation.Cursor(nextCursor);
            if (revision < 0 || graphRevision < 0 || graphRevision > revision || friendCount < 0 || friendCount > 200 || incomingCount < 0 || incomingCount > 50 || outgoingCount < 0 || outgoingCount > 50 || cacheExpiresAtMilliseconds < serverTimeMilliseconds || cacheExpiresAtMilliseconds - serverTimeMilliseconds > 60000)
                throw new ArgumentException("Invalid friends metadata.");
            if (operationId.HasValue)
            {
                FriendsValidation.Operation(operationId.Value);
                if (view != null || !FriendsValidation.Actions.Contains(resultCode) || nextCursor != null)
                    throw new ArgumentException("Invalid command receipt.");
            }
            else
            {
                FriendsValidation.View(view!);
                if (resultCode != null || request != null)
                    throw new ArgumentException("Invalid query receipt.");
            }
            if (view != "lookup")
            {
                if (relationship != null || relationshipRequestId.HasValue)
                    throw new ArgumentException("Unexpected relationship.");
            }
            else
            {
                if (player == null ? relationship != null || relationshipRequestId.HasValue : !new[] { "self", "none", "friend", "outgoing_pending", "incoming_pending" }.Contains(relationship))
                    throw new ArgumentException("Invalid lookup.");
                bool pending = relationship == "outgoing_pending" || relationship == "incoming_pending";
                if (pending != relationshipRequestId.HasValue)
                    throw new ArgumentException("Invalid pending lookup.");
                if (relationshipRequestId.HasValue)
                    FriendsValidation.Operation(relationshipRequestId.Value);
            }
            if (view == "incoming" && requests.Any(x => x.RecipientPlayerId != playerId) || view == "outgoing" && requests.Any(x => x.SenderPlayerId != playerId) || requests.Any(x => x.Status != "pending" || x.ExpiresAtMilliseconds <= serverTimeMilliseconds || x.Peer.PlayerId == playerId))
                throw new ArgumentException("Invalid request list.");
            if (view != "friends" && friends.Count != 0 || view != "incoming" && view != "outgoing" && requests.Count != 0 || view != "blocked" && blockedPlayers.Count != 0 || view != "suggestions" && suggestions.Count != 0 || !operationId.HasValue && view != "lookup" && player != null)
                throw new ArgumentException("Unexpected query fields.");
            AppId = appId;
            AccountId = accountId;
            PlayerId = playerId;
            ServerTimeMilliseconds = serverTimeMilliseconds;
            CacheExpiresAtMilliseconds = cacheExpiresAtMilliseconds;
            OperationId = operationId;
            View = view;
            Revision = revision;
            GraphRevision = graphRevision;
            MyPublicCode = myPublicCode;
            AllowRequests = allowRequests;
            FriendCount = friendCount;
            IncomingCount = incomingCount;
            OutgoingCount = outgoingCount;
            Friends = new ReadOnlyCollection<Friend>(friends.ToArray());
            Requests = new ReadOnlyCollection<FriendRequest>(requests.ToArray());
            BlockedPlayers = new ReadOnlyCollection<BlockedPlayer>(blockedPlayers.ToArray());
            Suggestions = new ReadOnlyCollection<PublicPlayer>(suggestions.ToArray());
            Player = player;
            Request = request;
            Relationship = relationship;
            RelationshipRequestId = relationshipRequestId;
            NextCursor = nextCursor;
            ResultCode = resultCode;
        }
        public Guid AppId
        {
            get;
        }
        public Guid AccountId
        {
            get;
        }
        public Guid PlayerId
        {
            get;
        }
        public long ServerTimeMilliseconds
        {
            get;
        }
        public long CacheExpiresAtMilliseconds
        {
            get;
        }
        public Guid? OperationId
        {
            get;
        }
        public string? View
        {
            get;
        }
        public long Revision
        {
            get;
        }
        public long GraphRevision
        {
            get;
        }
        public string MyPublicCode
        {
            get;
        }
        public bool AllowRequests
        {
            get;
        }
        public int FriendCount
        {
            get;
        }
        public int IncomingCount
        {
            get;
        }
        public int OutgoingCount
        {
            get;
        }
        public IReadOnlyList<Friend> Friends
        {
            get;
        }
        public IReadOnlyList<FriendRequest> Requests
        {
            get;
        }
        public IReadOnlyList<BlockedPlayer> BlockedPlayers
        {
            get;
        }
        public IReadOnlyList<PublicPlayer> Suggestions
        {
            get;
        }
        public PublicPlayer? Player
        {
            get;
        }
        public FriendRequest? Request
        {
            get;
        }
        public string? Relationship
        {
            get;
        }
        public Guid? RelationshipRequestId
        {
            get;
        }
        public string? NextCursor
        {
            get;
        }
        public string? ResultCode
        {
            get;
        }
    }
    internal static class FriendsValidation
    {
        internal static readonly string[] Views = { "mine", "friends", "incoming", "outgoing", "blocked", "suggestions", "lookup" };
        internal static readonly string[] Actions = { "send", "accept", "decline", "cancel", "remove", "block", "unblock", "set_privacy" };
        internal static void View(string value)
        {
            if (!Views.Contains(value))
                throw new ArgumentOutOfRangeException(nameof(value));
        }
        internal static void Id(Guid value)
        {
            if (value == Guid.Empty)
                throw new ArgumentException("Valid ID required.");
        }
        internal static void Operation(Guid value)
        {
            Id(value);
            if (value.ToString("D")[14] != '7')
                throw new ArgumentException("UUIDv7 required.");
        }
        internal static void Time(long value)
        {
            if (value < 0 || value > 253402300799999L)
                throw new ArgumentOutOfRangeException(nameof(value));
        }
        internal static void Code(string value)
        {
            if (value == null || !Regex.IsMatch(value, "^[0-9ABCDEFGHJKMNPQRSTVWXYZ]{12}$"))
                throw new ArgumentException("Invalid public code.");
        }
        internal static void Cursor(string? value)
        {
            if (value != null && (value.Length < 16 || value.Length > 4096 || !Regex.IsMatch(value, "^[A-Za-z0-9_-]+$")))
                throw new ArgumentException("Invalid cursor.");
        }
        internal static void Text(string value, int max, bool empty = false)
        {
            if (value == null || !empty && value.Length == 0)
                throw new ArgumentException("Invalid text.");
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsControl(c))
                    throw new ArgumentException("Invalid text.");
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= value.Length || !char.IsLowSurrogate(value[++i]))
                        throw new ArgumentException("Invalid text.");
                }
                else if (char.IsLowSurrogate(c))
                    throw new ArgumentException("Invalid text.");
                if (++count > max)
                    throw new ArgumentException("Invalid text.");
            }
        }
    }
}

