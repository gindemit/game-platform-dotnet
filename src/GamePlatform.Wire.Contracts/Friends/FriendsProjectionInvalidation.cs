using System;
namespace GamePlatform.Wire.Contracts.Friends
{
    public sealed class FriendsProjectionInvalidation : IProjectionChange
    {
        public FriendsProjectionInvalidation(Guid playerId, long revision)
        {
            if (playerId == Guid.Empty || revision <= 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            PlayerId = playerId;
            Revision = revision;
        }
        public Guid PlayerId
        {
            get;
        }
        public long Revision
        {
            get;
        }
        public string EntityType => "friends"; public string Kind => "invalidation";
    }
}

