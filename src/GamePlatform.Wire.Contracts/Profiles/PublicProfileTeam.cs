using System;

namespace GamePlatform.Wire.Contracts.Profiles
{
    public sealed class PublicProfileTeam
    {
        public PublicProfileTeam(Guid teamId, string name, string badgeId)
        {
            TeamId = teamId;
            Name = name;
            BadgeId = badgeId;
        }
        public Guid TeamId { get; }
        public string Name { get; }
        public string BadgeId { get; }
    }
}
