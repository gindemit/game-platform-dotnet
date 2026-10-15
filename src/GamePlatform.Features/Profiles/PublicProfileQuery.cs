using System;

namespace GamePlatform.Features.Profiles
{
    public sealed class PublicProfileQuery
    {
        public PublicProfileQuery(Guid subjectPlayerId)
        {
            if (subjectPlayerId == Guid.Empty)
                throw new ArgumentException("A subject player is required.", nameof(subjectPlayerId));
            SubjectPlayerId = subjectPlayerId;
        }
        public Guid SubjectPlayerId { get; }
    }
}
