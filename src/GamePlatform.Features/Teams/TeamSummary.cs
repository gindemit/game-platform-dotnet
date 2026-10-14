using System;

namespace GamePlatform.Features.Teams
{
    public sealed class TeamSummary
    {
        public TeamSummary(Guid teamId, string name, string description, string badgeId, string language, string joinPolicy, int requiredLevel, int capacity, int memberCount, long revision, long createdAtMilliseconds)
        {
            if (teamId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(teamId));
            TeamId = teamId;
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            Name = name;
            if (description == null)
                throw new ArgumentNullException(nameof(description));
            Description = description;
            if (badgeId == null)
                throw new ArgumentNullException(nameof(badgeId));
            BadgeId = badgeId;
            if (language == null)
                throw new ArgumentNullException(nameof(language));
            Language = language;
            if (joinPolicy == null)
                throw new ArgumentNullException(nameof(joinPolicy));
            JoinPolicy = joinPolicy;
            if (requiredLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(requiredLevel));
            RequiredLevel = requiredLevel;
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            if (memberCount < 0)
                throw new ArgumentOutOfRangeException(nameof(memberCount));
            MemberCount = memberCount;
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            Revision = revision;
            if (createdAtMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(createdAtMilliseconds));
            CreatedAtMilliseconds = createdAtMilliseconds;
        }
        public Guid TeamId { get; }
        public string Name { get; }
        public string Description { get; }
        public string BadgeId { get; }
        public string Language { get; }
        public string JoinPolicy { get; }
        public int RequiredLevel { get; }
        public int Capacity { get; }
        public int MemberCount { get; }
        public long Revision { get; }
        public long CreatedAtMilliseconds { get; }
    }

}
