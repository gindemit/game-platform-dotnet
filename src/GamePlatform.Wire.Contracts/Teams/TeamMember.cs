using System;

namespace GamePlatform.Wire.Contracts.Teams
{

    public sealed class TeamMember
    {
        public TeamMember(Guid teamId, Guid playerId, string role, long joinedAtMilliseconds, long helpCount, string displayName, string? avatarKey, long mutedUntilMilliseconds)
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
            if (displayName == null)
                throw new ArgumentNullException(nameof(displayName));
            DisplayName = displayName;
            AvatarKey = avatarKey;
            if (mutedUntilMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(mutedUntilMilliseconds));
            MutedUntilMilliseconds = mutedUntilMilliseconds;
        }
        public Guid TeamId { get; }
        public Guid PlayerId { get; }
        public string Role { get; }
        public long JoinedAtMilliseconds { get; }
        public long HelpCount { get; }
        public string DisplayName { get; }
        public string? AvatarKey { get; }
        public long MutedUntilMilliseconds { get; }
    }

}
