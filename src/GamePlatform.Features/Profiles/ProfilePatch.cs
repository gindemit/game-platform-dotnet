using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{

    /// <summary>A sparse semantic patch. Presence is distinct from explicit null for avatarKey.</summary>
    public sealed class ProfilePatch
    {
        public ProfilePatch(long expectedRevision, bool hasDisplayName, string? displayName,
            bool hasAvatarKey, string? avatarKey, bool hasLocale, string? locale)
        {
            if (expectedRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!hasDisplayName && !hasAvatarKey && !hasLocale)
                throw new ArgumentException("A profile patch requires one editable field.", nameof(hasDisplayName));
            if (hasDisplayName)
                ProfileValidation.DisplayName(displayName, nameof(displayName));
            else if (displayName != null)
                throw new ArgumentException("An absent display name cannot carry a value.", nameof(displayName));
            if (hasAvatarKey)
                ProfileValidation.AvatarKey(avatarKey, nameof(avatarKey));
            else if (avatarKey != null)
                throw new ArgumentException("An absent avatar key cannot carry a value.", nameof(avatarKey));
            if (hasLocale)
                ProfileValidation.Locale(locale, nameof(locale));
            else if (locale != null)
                throw new ArgumentException("An absent locale cannot carry a value.", nameof(locale));
            ExpectedRevision = expectedRevision;
            HasDisplayName = hasDisplayName;
            DisplayName = displayName;
            HasAvatarKey = hasAvatarKey;
            AvatarKey = avatarKey;
            HasLocale = hasLocale;
            Locale = locale;
        }

        public long ExpectedRevision { get; }
        public bool HasDisplayName { get; }
        public string? DisplayName { get; }
        public bool HasAvatarKey { get; }
        public string? AvatarKey { get; }
        public bool HasLocale { get; }
        public string? Locale { get; }
    }
}
