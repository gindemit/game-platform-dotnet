#nullable enable
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
            if (hasAvatarKey) ProfileValidation.AvatarKey(avatarKey, nameof(avatarKey));
            else if (avatarKey != null) throw new ArgumentException("An absent avatar key cannot carry a value.", nameof(avatarKey));
            if (hasLocale) ProfileValidation.Locale(locale, nameof(locale));
            else if (locale != null) throw new ArgumentException("An absent locale cannot carry a value.", nameof(locale));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            ProfileValidation.Timestamp(updatedAtMilliseconds, nameof(updatedAtMilliseconds));
            if (extensions.Length > ProfileValidation.MaximumExtensionBytes) throw new ArgumentOutOfRangeException(nameof(extensions));
            DisplayName = displayName; HasAvatarKey = hasAvatarKey; AvatarKey = avatarKey;
            HasLocale = hasLocale; Locale = locale; Revision = revision; UpdatedAtMilliseconds = updatedAtMilliseconds;
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

    /// <summary>A sparse semantic patch. Presence is distinct from explicit null for avatarKey.</summary>
    public sealed class ProfilePatch
    {
        public ProfilePatch(long expectedRevision, bool hasDisplayName, string? displayName,
            bool hasAvatarKey, string? avatarKey, bool hasLocale, string? locale)
        {
            if (expectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (!hasDisplayName && !hasAvatarKey && !hasLocale) throw new ArgumentException("A profile patch requires one editable field.", nameof(hasDisplayName));
            if (hasDisplayName) ProfileValidation.DisplayName(displayName, nameof(displayName));
            else if (displayName != null) throw new ArgumentException("An absent display name cannot carry a value.", nameof(displayName));
            if (hasAvatarKey) ProfileValidation.AvatarKey(avatarKey, nameof(avatarKey));
            else if (avatarKey != null) throw new ArgumentException("An absent avatar key cannot carry a value.", nameof(avatarKey));
            if (hasLocale) ProfileValidation.Locale(locale, nameof(locale));
            else if (locale != null) throw new ArgumentException("An absent locale cannot carry a value.", nameof(locale));
            ExpectedRevision = expectedRevision; HasDisplayName = hasDisplayName; DisplayName = displayName;
            HasAvatarKey = hasAvatarKey; AvatarKey = avatarKey; HasLocale = hasLocale; Locale = locale;
        }

        public long ExpectedRevision { get; }
        public bool HasDisplayName { get; }
        public string? DisplayName { get; }
        public bool HasAvatarKey { get; }
        public string? AvatarKey { get; }
        public bool HasLocale { get; }
        public string? Locale { get; }
    }

    public enum ProfilePendingStatus { AwaitingReceipt = 0, AcceptedAwaitingPull = 1, Conflict = 2 }

    /// <summary>Durable local intent, never a claim that the server accepted the profile fields.</summary>
    public sealed class PendingProfileEdit
    {
        public PendingProfileEdit(OperationId operationId, ClientStreamId streamId, string businessSource, ProfilePatch patch,
            long localRevision, ProfilePendingStatus status, long? acceptedRevision)
        {
            if (!operationId.IsValid) throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (!streamId.IsValid) throw new ArgumentException("A valid stream identity is required.", nameof(streamId));
            ProfileValidation.BusinessSource(businessSource, nameof(businessSource));
            Patch = patch ?? throw new ArgumentNullException(nameof(patch));
            if (localRevision <= 0) throw new ArgumentOutOfRangeException(nameof(localRevision));
            if (!Enum.IsDefined(typeof(ProfilePendingStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (status == ProfilePendingStatus.AcceptedAwaitingPull && (!acceptedRevision.HasValue || acceptedRevision.Value <= patch.ExpectedRevision))
                throw new ArgumentException("An accepted pending edit requires a newer confirmed revision.", nameof(acceptedRevision));
            if (status != ProfilePendingStatus.AcceptedAwaitingPull && acceptedRevision.HasValue)
                throw new ArgumentException("Only an accepted pending edit carries an accepted revision.", nameof(acceptedRevision));
            OperationId = operationId; StreamId = streamId; BusinessSource = businessSource; LocalRevision = localRevision;
            Patch = patch; Status = status; AcceptedRevision = acceptedRevision;
        }

        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public ProfilePatch Patch { get; }
        public long LocalRevision { get; }
        public ProfilePendingStatus Status { get; }
        public long? AcceptedRevision { get; }
    }

    /// <summary>Immutable observation combining the last confirmed projection and (if present) a local intent.</summary>
    public sealed class ProfileSnapshot
    {
        public ProfileSnapshot(ProfileConfirmed? confirmed, PendingProfileEdit? pending)
        {
            Confirmed = confirmed; Pending = pending;
        }
        public ProfileConfirmed? Confirmed { get; }
        public PendingProfileEdit? Pending { get; }
    }

    public sealed class ProfileUpdateRequest
    {
        public ProfileUpdateRequest(ScopedOwnerContext owner, OperationId operationId, ClientStreamId streamId,
            string businessSource, ProfilePatch patch)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (!streamId.IsValid) throw new ArgumentException("A valid stream identity is required.", nameof(streamId));
            ProfileValidation.BusinessSource(businessSource, nameof(businessSource));
            Owner = owner; OperationId = operationId; StreamId = streamId; BusinessSource = businessSource;
            Patch = patch ?? throw new ArgumentNullException(nameof(patch));
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public ProfilePatch Patch { get; }
    }

    /// <summary>Receipt-derived outcome. It may only move a matching local command to awaiting-pull.</summary>
    public sealed class ProfileUpdateAcceptance
    {
        public ProfileUpdateAcceptance(ScopedOwnerContext owner, OperationId operationId, long resultingRevision)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (resultingRevision < 1) throw new ArgumentOutOfRangeException(nameof(resultingRevision));
            Owner = owner; OperationId = operationId; ResultingRevision = resultingRevision;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public long ResultingRevision { get; }
    }

    /// <summary>Feature-owned remote read port. An adapter must bind it to the frozen getProfile operation.</summary>
    public interface IProfileRemote
    {
        Task<ProfileRemoteRead> ReadAsync(ScopedOwnerContext owner, CancellationToken cancellationToken);
    }

    public sealed class ProfileRemoteRead
    {
        public ProfileRemoteRead(ScopedOwnerContext owner, ProfileConfirmed profile)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            Owner = owner; Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }
        public ScopedOwnerContext Owner { get; }
        public ProfileConfirmed Profile { get; }
    }

    /// <summary>Codec boundary for the local known-field records and frozen semantic command body.</summary>
    public interface IProfileStateCodec
    {
        byte[] EncodeConfirmed(ProfileConfirmed profile);
        ProfileConfirmed DecodeConfirmed(long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions);
        byte[] EncodePending(PendingProfileEdit pending);
        PendingProfileEdit DecodePending(long localRevision, ReadOnlySpan<byte> payload);
        byte[] EncodePatchCommand(ProfilePatch patch);
        void ValidateExtensions(ReadOnlySpan<byte> extensions);
    }

    public sealed class ProfileConflictException : InvalidOperationException
    {
        public ProfileConflictException(string message) : base(message) { }
    }

    public sealed class ProfileOwnerMismatchException : InvalidOperationException
    {
        public ProfileOwnerMismatchException() : base("The profile result does not belong to the captured account/view generation.") { }
    }

    internal static class ProfileValidation
    {
        internal const int MaximumExtensionBytes = 16_384;
        private static readonly Regex SemanticKey = new Regex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$", RegexOptions.CultureInvariant);
        private static readonly Regex LocalePattern = new Regex("^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant);

        internal static void DisplayName(string? value, string parameter)
        {
            if (value == null || value.Length == 0 || !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                !string.Equals(value, value.Normalize(NormalizationForm.FormC), StringComparison.Ordinal) || ScalarCount(value) > 64)
                throw new ArgumentOutOfRangeException(parameter);
            ValidateUnicode(value, parameter);
        }
        internal static void AvatarKey(string? value, string parameter)
        {
            if (value != null && !SemanticKey.IsMatch(value)) throw new ArgumentOutOfRangeException(parameter);
        }
        internal static void Locale(string? value, string parameter)
        {
            if (value == null || !LocalePattern.IsMatch(value) || ScalarCount(value) > 35) throw new ArgumentOutOfRangeException(parameter);
            ValidateUnicode(value, parameter);
        }
        internal static void Timestamp(long value, string parameter)
        {
            if (value < 0 || value > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(parameter);
        }
        internal static void BusinessSource(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) throw new ArgumentOutOfRangeException(parameter);
        }
        private static int ScalarCount(string value)
        {
            var count = 0;
            for (var index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index])) { if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1])) return int.MaxValue; index++; }
                else if (char.IsLowSurrogate(value[index])) return int.MaxValue;
                count++;
            }
            return count;
        }
        private static void ValidateUnicode(string value, string parameter)
        {
            if (ScalarCount(value) == int.MaxValue || Encoding.UTF8.GetByteCount(value) > 8192) throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
