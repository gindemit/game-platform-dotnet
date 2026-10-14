using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
namespace GamePlatform.Wire.Contracts.Friends
{
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
        public Guid AppId { get; }
        public Guid AccountId { get; }
        public Guid PlayerId { get; }
        public long ServerTimeMilliseconds { get; }
        public long CacheExpiresAtMilliseconds { get; }
        public Guid? OperationId { get; }
        public string? View { get; }
        public long Revision { get; }
        public long GraphRevision { get; }
        public string MyPublicCode { get; }
        public bool AllowRequests { get; }
        public int FriendCount { get; }
        public int IncomingCount { get; }
        public int OutgoingCount { get; }
        public IReadOnlyList<Friend> Friends { get; }
        public IReadOnlyList<FriendRequest> Requests { get; }
        public IReadOnlyList<BlockedPlayer> BlockedPlayers { get; }
        public IReadOnlyList<PublicPlayer> Suggestions { get; }
        public PublicPlayer? Player { get; }
        public FriendRequest? Request { get; }
        public string? Relationship { get; }
        public Guid? RelationshipRequestId { get; }
        public string? NextCursor { get; }
        public string? ResultCode { get; }
    }
}
