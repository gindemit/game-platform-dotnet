using System;

namespace GamePlatform.Features.Profiles
{
    public sealed class PublicProfile
    {
        public PublicProfile(string displayName, string? avatarKey, string? locale, long revision, PublicProfileTeam? team)
        {
            ProfileValidation.DisplayName(displayName, nameof(displayName));
            ProfileValidation.AvatarKey(avatarKey, nameof(avatarKey));
            if (locale != null)
                ProfileValidation.Locale(locale, nameof(locale));
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            DisplayName = displayName;
            AvatarKey = avatarKey;
            Locale = locale;
            Revision = revision;
            Team = team;
        }
        public string DisplayName { get; }
        public string? AvatarKey { get; }
        public string? Locale { get; }
        public long Revision { get; }
        public PublicProfileTeam? Team { get; }
    }
}
