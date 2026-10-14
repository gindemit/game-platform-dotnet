using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
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

