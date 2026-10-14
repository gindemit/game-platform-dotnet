using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
namespace GamePlatform.Features.Friends
{
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
        public Guid RequestId { get; }
        public Guid SenderPlayerId { get; }
        public Guid RecipientPlayerId { get; }
        public string Status { get; }
        public long CreatedAtMilliseconds { get; }
        public long ExpiresAtMilliseconds { get; }
        public PublicPlayer Peer { get; }
    }
}

