using System;
namespace GamePlatform.Features.Friends
{
    public sealed class Friend
    {
        public Friend(PublicPlayer player, long sinceMilliseconds)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            FriendsValidation.Time(sinceMilliseconds);
            Player = player;
            SinceMilliseconds = sinceMilliseconds;
        }
        public PublicPlayer Player { get; }
        public long SinceMilliseconds { get; }
    }
}
