using System;

namespace GamePlatform.Wire.Contracts.Profiles
{
    public sealed class PublicProfileResponse
    {
        public PublicProfileResponse(Guid appId, Guid playerId, long serverTime, PublicProfile? profile, PublicProfileTeam? team)
        {
            AppId = appId;
            PlayerId = playerId;
            ServerTime = serverTime;
            Profile = profile;
            Team = team;
        }
        public Guid AppId { get; }
        public Guid PlayerId { get; }
        public long ServerTime { get; }
        public PublicProfile? Profile { get; }
        public PublicProfileTeam? Team { get; }
    }
}
