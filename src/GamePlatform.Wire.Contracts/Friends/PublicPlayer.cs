using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
namespace GamePlatform.Wire.Contracts.Friends
{
    public sealed class PublicPlayer
    {
        public PublicPlayer(Guid playerId, string displayName, string? avatarKey)
        {
            FriendsValidation.Id(playerId);
            FriendsValidation.Text(displayName, 64);
            if (avatarKey != null)
                FriendsValidation.Text(avatarKey, 128, true);
            PlayerId = playerId;
            DisplayName = displayName;
            AvatarKey = avatarKey;
        }
        public Guid PlayerId { get; }
        public string DisplayName { get; }
        public string? AvatarKey { get; }
    }
}

