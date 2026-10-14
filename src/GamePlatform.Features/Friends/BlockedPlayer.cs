using System;
namespace GamePlatform.Features.Friends
{
    public sealed class BlockedPlayer
    {
        public BlockedPlayer(PublicPlayer player, long blockedAtMilliseconds)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            FriendsValidation.Time(blockedAtMilliseconds);
            Player = player;
            BlockedAtMilliseconds = blockedAtMilliseconds;
        }
        public PublicPlayer Player { get; }
        public long BlockedAtMilliseconds { get; }
    }
}
