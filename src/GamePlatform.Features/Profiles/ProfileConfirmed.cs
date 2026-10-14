using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{
    /// <summary>Known, player-editable profile fields. Server audit fields are deliberately not represented here.</summary>
    public sealed class ProfileConfirmed
    {
        private readonly byte[] extensions;

        public ProfileConfirmed(string displayName, bool hasAvatarKey, string? avatarKey, bool hasLocale, string? locale,
            long revision, long updatedAtMilliseconds, ReadOnlySpan<byte> extensions)
        {
            ProfileValidation.DisplayName(displayName, nameof(displayName));
            if (hasAvatarKey)
                ProfileValidation.AvatarKey(avatarKey, nameof(avatarKey));
            else if (avatarKey != null)
                throw new ArgumentException("An absent avatar key cannot carry a value.", nameof(avatarKey));
            if (hasLocale)
                ProfileValidation.Locale(locale, nameof(locale));
            else if (locale != null)
                throw new ArgumentException("An absent locale cannot carry a value.", nameof(locale));
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            ProfileValidation.Timestamp(updatedAtMilliseconds, nameof(updatedAtMilliseconds));
            if (extensions.Length > ProfileValidation.MaximumExtensionBytes)
                throw new ArgumentOutOfRangeException(nameof(extensions));
            DisplayName = displayName;
            HasAvatarKey = hasAvatarKey;
            AvatarKey = avatarKey;
            HasLocale = hasLocale;
            Locale = locale;
            Revision = revision;
            UpdatedAtMilliseconds = updatedAtMilliseconds;
            this.extensions = extensions.ToArray();
        }

        public string DisplayName { get; }
        public bool HasAvatarKey { get; }
        public string? AvatarKey { get; }
        public bool HasLocale { get; }
        public string? Locale { get; }
        public long Revision { get; }
        public long UpdatedAtMilliseconds { get; }
        /// <summary>Opaque, already validated supported-extension bytes. Callers receive a defensive copy.</summary>
        public byte[] CopyExtensions() => (byte[])extensions.Clone();
    }
}
