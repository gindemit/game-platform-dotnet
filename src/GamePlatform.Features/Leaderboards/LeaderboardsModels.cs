#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace GamePlatform.Features.Leaderboards
{
    public sealed class LeaderboardEntry
    {
        public LeaderboardEntry(Guid participantId, long rank, long score, string displayName, string? avatarKey = null,
            string? badgeId = null, string? countryCode = null)
        {
            if (participantId == Guid.Empty || rank < 1 || rank > 500 || score < 1) throw new ArgumentOutOfRangeException(nameof(rank));
            LeaderboardsValidation.Text(displayName, 64, nameof(displayName));
            if (avatarKey != null) LeaderboardsValidation.Text(avatarKey, 128, nameof(avatarKey), true);
            if (badgeId != null) LeaderboardsValidation.Text(badgeId, 48, nameof(badgeId), true);
            LeaderboardsValidation.Country(countryCode, true);
            ParticipantId = participantId; Rank = rank; Score = score; DisplayName = displayName;
            AvatarKey = avatarKey; BadgeId = badgeId; CountryCode = countryCode;
        }
        public Guid ParticipantId { get; }
        public long Rank { get; }
        public long Score { get; }
        public string DisplayName { get; }
        public string? AvatarKey { get; }
        public string? BadgeId { get; }
        public string? CountryCode { get; }
    }

    public sealed class LeaderboardsCountrySelection
    {
        public LeaderboardsCountrySelection(string? currentCountryCode, string? pendingCountryCode, long? pendingEffectiveAt)
        {
            LeaderboardsValidation.Country(currentCountryCode, true); LeaderboardsValidation.Country(pendingCountryCode, true);
            if ((pendingCountryCode == null) != (pendingEffectiveAt == null)) throw new ArgumentException("Pending country and time must be present together.");
            if (pendingEffectiveAt.HasValue) LeaderboardsValidation.Time(pendingEffectiveAt.Value);
            CurrentCountryCode = currentCountryCode; PendingCountryCode = pendingCountryCode; PendingEffectiveAt = pendingEffectiveAt;
        }
        public string? CurrentCountryCode { get; }
        public string? PendingCountryCode { get; }
        public long? PendingEffectiveAt { get; }
    }

    public sealed class LeaderboardsResponse
    {
        public LeaderboardsResponse(string boardId, string participantKind, string scope, string? countryCode,
            string periodId, long periodStart, long periodEnd, long resetAt, Guid snapshotId, long snapshotCreatedAt,
            long expiresAt, string scoreAuthority, IReadOnlyList<LeaderboardEntry> entries, string? nextCursor,
            LeaderboardEntry? self, int snapshotLimit, long serverTime, LeaderboardsCountrySelection? countrySelection)
        {
            LeaderboardsValidation.Board(boardId); LeaderboardsValidation.Kind(participantKind);
            LeaderboardsValidation.Scope(scope, countryCode); LeaderboardsValidation.Text(periodId, 32, nameof(periodId), true);
            foreach (long value in new[] { periodStart, periodEnd, resetAt, snapshotCreatedAt, expiresAt, serverTime }) LeaderboardsValidation.Time(value);
            if (periodEnd <= periodStart || resetAt != periodEnd || expiresAt <= snapshotCreatedAt || snapshotId == Guid.Empty || snapshotId.ToString("D")[14] != '7')
                throw new ArgumentException("Invalid period or snapshot boundary.");
            if (scoreAuthority != "client_trusted_unvalidated" || snapshotLimit != 500 || entries == null || entries.Count > 50 || entries.Any(x => x == null))
                throw new ArgumentException("Invalid bounded standings.");
            LeaderboardsValidation.Cursor(nextCursor);
            if (entries.Select(x => x.ParticipantId).Distinct().Count() != entries.Count || entries.Select(x => x.Rank).Distinct().Count() != entries.Count)
                throw new ArgumentException("Duplicate snapshot entry.");
            for (int i = 1; i < entries.Count; i++)
                if (entries[i].Rank != entries[i - 1].Rank + 1 || entries[i].Score > entries[i - 1].Score ||
                    entries[i].Score == entries[i - 1].Score && string.CompareOrdinal(entries[i].ParticipantId.ToString("D"), entries[i - 1].ParticipantId.ToString("D")) <= 0)
                    throw new ArgumentException("Invalid snapshot ordering.");
            if (scope == "country" && (entries.Any(x => x.CountryCode != countryCode) || self != null && self.CountryCode != countryCode))
                throw new ArgumentException("Country standings mismatch.");
            var matching = self == null ? null : entries.FirstOrDefault(x => x.ParticipantId == self.ParticipantId);
            if (matching != null && (matching.Rank != self!.Rank || matching.Score != self.Score || matching.DisplayName != self.DisplayName ||
                matching.AvatarKey != self.AvatarKey || matching.BadgeId != self.BadgeId || matching.CountryCode != self.CountryCode))
                throw new ArgumentException("Self differs from the snapshot row.");
            BoardId = boardId; ParticipantKind = participantKind; Scope = scope; CountryCode = countryCode;
            PeriodId = periodId; PeriodStart = periodStart; PeriodEnd = periodEnd; ResetAt = resetAt; SnapshotId = snapshotId;
            SnapshotCreatedAt = snapshotCreatedAt; ExpiresAt = expiresAt; ScoreAuthority = scoreAuthority;
            Entries = new ReadOnlyCollection<LeaderboardEntry>(entries.ToArray()); NextCursor = nextCursor; Self = self;
            SnapshotLimit = snapshotLimit; ServerTime = serverTime; CountrySelection = countrySelection;
        }
        public string BoardId { get; }
        public string ParticipantKind { get; }
        public string Scope { get; }
        public string? CountryCode { get; }
        public string PeriodId { get; }
        public long PeriodStart { get; }
        public long PeriodEnd { get; }
        public long ResetAt { get; }
        public Guid SnapshotId { get; }
        public long SnapshotCreatedAt { get; }
        public long ExpiresAt { get; }
        public string ScoreAuthority { get; }
        public IReadOnlyList<LeaderboardEntry> Entries { get; }
        public string? NextCursor { get; }
        public LeaderboardEntry? Self { get; }
        public int SnapshotLimit { get; }
        public long ServerTime { get; }
        public LeaderboardsCountrySelection? CountrySelection { get; }
    }

    public sealed class LeaderboardsCountryResponse
    {
        public LeaderboardsCountryResponse(Guid operationId, string boardId, string participantKind, Guid participantId, string countryCode, long effectiveAt)
        {
            if (operationId == Guid.Empty || participantId == Guid.Empty) throw new ArgumentException("Valid identities are required.");
            LeaderboardsValidation.Board(boardId); LeaderboardsValidation.Kind(participantKind);
            LeaderboardsValidation.Country(countryCode, false); LeaderboardsValidation.Time(effectiveAt);
            OperationId = operationId; BoardId = boardId; ParticipantKind = participantKind;
            ParticipantId = participantId; CountryCode = countryCode; EffectiveAt = effectiveAt;
        }
        public Guid OperationId { get; }
        public string BoardId { get; }
        public string ParticipantKind { get; }
        public Guid ParticipantId { get; }
        public string CountryCode { get; }
        public long EffectiveAt { get; }
    }

    internal static class LeaderboardsValidation
    {
        internal static void Board(string value) { if (value == null || !Regex.IsMatch(value, "^[a-z][a-z0-9-]{0,63}$")) throw new ArgumentOutOfRangeException(nameof(value)); }
        internal static void Kind(string value) { if (value != "player" && value != "team") throw new ArgumentOutOfRangeException(nameof(value)); }
        internal static void Country(string? value, bool nullable) { if (value == null ? !nullable : !Regex.IsMatch(value, "^[A-Z]{2}$")) throw new ArgumentOutOfRangeException(nameof(value)); }
        internal static void Scope(string scope, string? country) { if (scope != "global" && scope != "country" || (scope == "country") != (country != null)) throw new ArgumentException("Invalid scope country binding."); Country(country, true); }
        internal static void Cursor(string? value) { if (value != null) Text(value, 4096, nameof(value)); if (value != null && (value.Length < 16 || !Regex.IsMatch(value, "^[A-Za-z0-9_-]+$"))) throw new ArgumentOutOfRangeException(nameof(value)); }
        internal static void Time(long value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); }
        internal static void Text(string value, int maximum, string name, bool emptyAllowed = false)
        {
            if (value == null || !emptyAllowed && value.Length == 0) throw new ArgumentOutOfRangeException(name);
            int scalars = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsControl(c)) throw new ArgumentOutOfRangeException(name);
                if (char.IsHighSurrogate(c)) { if (i + 1 >= value.Length || !char.IsLowSurrogate(value[++i])) throw new ArgumentOutOfRangeException(name); }
                else if (char.IsLowSurrogate(c)) throw new ArgumentOutOfRangeException(name);
                if (++scalars > maximum) throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
