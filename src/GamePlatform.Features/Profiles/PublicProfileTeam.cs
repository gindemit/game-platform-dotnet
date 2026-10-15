using System;
using System.Linq;
using GamePlatform.Features.Leaderboards;

namespace GamePlatform.Features.Profiles
{
    public sealed class PublicProfileTeam
    {
        public PublicProfileTeam(Guid teamId, string name, string badgeId)
        {
            if (teamId == Guid.Empty)
                throw new ArgumentException("A team is required.", nameof(teamId));
            LeaderboardsValidation.Text(name, 24, nameof(name));
            if (name.Length - name.Count(char.IsLowSurrogate) < 3)
                throw new ArgumentOutOfRangeException(nameof(name));
            LeaderboardsValidation.Text(badgeId, 48, nameof(badgeId));
            TeamId = teamId;
            Name = name;
            BadgeId = badgeId;
        }
        public Guid TeamId { get; }
        public string Name { get; }
        public string BadgeId { get; }
    }
}
