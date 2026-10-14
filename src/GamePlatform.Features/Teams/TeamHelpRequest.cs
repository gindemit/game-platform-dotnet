using System;

namespace GamePlatform.Features.Teams
{

    public sealed class TeamHelpRequest
    {
        public TeamHelpRequest(Guid requestId, Guid teamId, Guid playerId, string displayName, int currentCount, int requiredCount, long createdAtMilliseconds, long expiresAtMilliseconds, bool contributedByMe, string status)
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
            if (currentCount < 0)
                throw new ArgumentOutOfRangeException(nameof(currentCount));
            CurrentCount = currentCount;
            if (requiredCount < 0)
                throw new ArgumentOutOfRangeException(nameof(requiredCount));
            RequiredCount = requiredCount;
            if (createdAtMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(createdAtMilliseconds));
            CreatedAtMilliseconds = createdAtMilliseconds;
            if (expiresAtMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(expiresAtMilliseconds));
            ExpiresAtMilliseconds = expiresAtMilliseconds;
            ContributedByMe = contributedByMe;
            if (status == null)
                throw new ArgumentNullException(nameof(status));
            Status = status;
        }
        public Guid RequestId { get; }
        public Guid TeamId { get; }
        public Guid PlayerId { get; }
        public string DisplayName { get; }
        public int CurrentCount { get; }
        public int RequiredCount { get; }
        public long CreatedAtMilliseconds { get; }
        public long ExpiresAtMilliseconds { get; }
        public bool ContributedByMe { get; }
        public string Status { get; }
    }

}
