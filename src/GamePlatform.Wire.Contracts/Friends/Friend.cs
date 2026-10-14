using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
namespace GamePlatform.Wire.Contracts.Friends
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
