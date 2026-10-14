using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GamePlatform.Wire.Contracts.Leaderboards
{

    public sealed class LeaderboardsResponse
    {
        public LeaderboardsResponse(string boardId, string participantKind, string scope, string? countryCode,
            string periodId, long periodStart, long periodEnd, long resetAt, Guid snapshotId, long snapshotCreatedAt,
            long expiresAt, string scoreAuthority, IReadOnlyList<LeaderboardEntry> entries, string? nextCursor,
            LeaderboardEntry? self, int snapshotLimit, long serverTime, LeaderboardsCountrySelection? countrySelection)
        {
            LeaderboardsValidation.Board(boardId);
            LeaderboardsValidation.Kind(participantKind);
            LeaderboardsValidation.Scope(scope, countryCode);
            LeaderboardsValidation.Text(periodId, 32, nameof(periodId), true);
            foreach (long value in new[] { periodStart, periodEnd, resetAt, snapshotCreatedAt, expiresAt, serverTime })
                LeaderboardsValidation.Time(value);
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
            BoardId = boardId;
            ParticipantKind = participantKind;
            Scope = scope;
            CountryCode = countryCode;
            PeriodId = periodId;
            PeriodStart = periodStart;
            PeriodEnd = periodEnd;
            ResetAt = resetAt;
            SnapshotId = snapshotId;
            SnapshotCreatedAt = snapshotCreatedAt;
            ExpiresAt = expiresAt;
            ScoreAuthority = scoreAuthority;
            Entries = new ReadOnlyCollection<LeaderboardEntry>(entries.ToArray());
            NextCursor = nextCursor;
            Self = self;
            SnapshotLimit = snapshotLimit;
            ServerTime = serverTime;
            CountrySelection = countrySelection;
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
}
