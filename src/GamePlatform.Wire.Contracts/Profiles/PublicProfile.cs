namespace GamePlatform.Wire.Contracts.Profiles
{
    public sealed class PublicProfile
    {
        public PublicProfile(string displayName, string? avatarKey, string? locale, long revision)
        {
            DisplayName = displayName;
            AvatarKey = avatarKey;
            Locale = locale;
            Revision = revision;
        }
        public string DisplayName { get; }
        public string? AvatarKey { get; }
        public string? Locale { get; }
        public long Revision { get; }
    }
}
