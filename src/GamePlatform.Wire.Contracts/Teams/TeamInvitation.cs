using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlatform.Wire.Contracts.Teams
{

    public sealed class TeamInvitation
    {
        public TeamInvitation(Guid inviteId, Guid teamId, Guid playerId, Guid inviterPlayerId, string status, long createdAtMilliseconds, long expiresAtMilliseconds)
        {
            if (inviteId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(inviteId));
            InviteId = inviteId;
            if (teamId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(teamId));
            TeamId = teamId;
            if (playerId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (inviterPlayerId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(inviterPlayerId));
            InviterPlayerId = inviterPlayerId;
            if (status == null)
                throw new ArgumentNullException(nameof(status));
            Status = status;
            if (createdAtMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(createdAtMilliseconds));
            CreatedAtMilliseconds = createdAtMilliseconds;
            if (expiresAtMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(expiresAtMilliseconds));
            ExpiresAtMilliseconds = expiresAtMilliseconds;
        }
        public Guid InviteId { get; }
        public Guid TeamId { get; }
        public Guid PlayerId { get; }
        public Guid InviterPlayerId { get; }
        public string Status { get; }
        public long CreatedAtMilliseconds { get; }
        public long ExpiresAtMilliseconds { get; }
    }

}
