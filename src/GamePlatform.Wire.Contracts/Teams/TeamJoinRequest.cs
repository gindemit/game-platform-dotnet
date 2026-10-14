using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlatform.Wire.Contracts.Teams
{

    public sealed class TeamJoinRequest
    {
        public TeamJoinRequest(Guid requestId, Guid teamId, Guid playerId, string displayName, string status, long createdAtMilliseconds)
        {
            if (requestId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(requestId));
            RequestId = requestId;
            if (teamId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(teamId));
            TeamId = teamId;
            if (playerId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (displayName == null)
                throw new ArgumentNullException(nameof(displayName));
            DisplayName = displayName;
            if (status == null)
                throw new ArgumentNullException(nameof(status));
            Status = status;
            if (createdAtMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(createdAtMilliseconds));
            CreatedAtMilliseconds = createdAtMilliseconds;
        }
        public Guid RequestId { get; }
        public Guid TeamId { get; }
        public Guid PlayerId { get; }
        public string DisplayName { get; }
        public string Status { get; }
        public long CreatedAtMilliseconds { get; }
    }

}
