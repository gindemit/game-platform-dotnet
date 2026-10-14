using System;

namespace GamePlatform.Features.Teams
{

    public sealed class TeamMembership
    {
        public TeamMembership(Guid teamId, Guid playerId, string role, long joinedAtMilliseconds, long helpCount)
        {
            if (teamId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(teamId));
            TeamId = teamId;
            if (playerId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (role == null)
                throw new ArgumentNullException(nameof(role));
            Role = role;
            if (joinedAtMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(joinedAtMilliseconds));
            JoinedAtMilliseconds = joinedAtMilliseconds;
            if (helpCount < 0)
                throw new ArgumentOutOfRangeException(nameof(helpCount));
            HelpCount = helpCount;
        }
        public Guid TeamId { get; }
        public Guid PlayerId { get; }
        public string Role { get; }
        public long JoinedAtMilliseconds { get; }
        public long HelpCount { get; }
    }

}
