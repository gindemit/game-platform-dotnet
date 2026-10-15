using System;

namespace GamePlatform.Features.Profiles
{
    public sealed class PublicProfileResponse
    {
        public PublicProfileResponse(Guid appId, Guid playerId, long serverTimeMilliseconds, PublicProfile? profile)
        {
            if (appId == Guid.Empty || playerId == Guid.Empty)
                throw new ArgumentException("App and player identities are required.");
            ProfileValidation.Timestamp(serverTimeMilliseconds, nameof(serverTimeMilliseconds));
            AppId = appId;
            PlayerId = playerId;
            ServerTimeMilliseconds = serverTimeMilliseconds;
            Profile = profile;
        }
        public Guid AppId { get; }
        public Guid PlayerId { get; }
        public long ServerTimeMilliseconds { get; }
        /// <summary>
        /// Null when the subject is unavailable.
        /// </summary>
        public PublicProfile? Profile { get; }
    }
}
