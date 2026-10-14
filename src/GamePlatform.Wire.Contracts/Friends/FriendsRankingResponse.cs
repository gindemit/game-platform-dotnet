using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace GamePlatform.Wire.Contracts.Friends
{

    public sealed class FriendsRankingResponse
    {
        public FriendsRankingResponse(string boardId, string participantKind, string scope, string? countryCode,
            string periodId, long periodStart, long periodEnd, long resetAt, Guid snapshotId, long snapshotCreatedAt,
            long expiresAt, string scoreAuthority, IReadOnlyList<FriendsRankingEntry> entries, string? nextCursor,
            FriendsRankingEntry? self, int snapshotLimit, long serverTime, FriendsRankingCountrySelection? countrySelection,
            Guid? appId = null, Guid? accountId = null, Guid? playerId = null, long? graphRevision = null, int? cohortSize = null)
        {
            FriendsRankingValidation.Board(boardId);
            FriendsRankingValidation.Kind(participantKind);
            FriendsRankingValidation.Scope(scope, countryCode);
            FriendsRankingValidation.Text(periodId, 32, nameof(periodId), true);
            foreach (long value in new[] { periodStart, periodEnd, resetAt, snapshotCreatedAt, expiresAt, serverTime })
                FriendsRankingValidation.Time(value);
            if (periodEnd <= periodStart || resetAt != periodEnd || expiresAt <= snapshotCreatedAt || snapshotId == Guid.Empty || snapshotId.ToString("D")[14] != '7')
                throw new ArgumentException("Invalid period or snapshot boundary.");
            if (scoreAuthority != "client_trusted_unvalidated" || scope != "friends" && snapshotLimit != 500 || entries == null || entries.Count > 50 || entries.Any(x => x == null))
                throw new ArgumentException("Invalid bounded standings.");
            if (scope == "friends")
            {
                if (participantKind != "player" || !boardId.StartsWith("players-", StringComparison.Ordinal) || !appId.HasValue || appId == Guid.Empty || !accountId.HasValue || accountId == Guid.Empty || !playerId.HasValue || playerId == Guid.Empty || !graphRevision.HasValue || graphRevision < 0 || !cohortSize.HasValue || cohortSize < 1 || cohortSize > snapshotLimit || snapshotLimit < 2 || snapshotLimit > 201 || self == null || self.ParticipantId != playerId || self.Rank > cohortSize || self.BadgeId != null || entries.Any(x => x.Rank > cohortSize || x.BadgeId != null) || countrySelection != null)
                    throw new ArgumentException("Invalid friends cohort.");
            }
            else if (appId.HasValue || accountId.HasValue || playerId.HasValue || graphRevision.HasValue || cohortSize.HasValue || entries.Any(x => x.Score < 1) || self != null && self.Score < 1)
                throw new ArgumentException("Invalid global/country standings.");
            FriendsRankingValidation.Cursor(nextCursor);
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
            Entries = new ReadOnlyCollection<FriendsRankingEntry>(entries.ToArray());
            NextCursor = nextCursor;
            Self = self;
            SnapshotLimit = snapshotLimit;
            ServerTime = serverTime;
            CountrySelection = countrySelection;
            AppId = appId;
            AccountId = accountId;
            PlayerId = playerId;
            GraphRevision = graphRevision;
            CohortSize = cohortSize;
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
        public IReadOnlyList<FriendsRankingEntry> Entries { get; }
        public string? NextCursor { get; }
        public FriendsRankingEntry? Self { get; }
        public int SnapshotLimit { get; }
        public long ServerTime { get; }
        public FriendsRankingCountrySelection? CountrySelection { get; }
        public Guid? AppId { get; }
        public Guid? AccountId { get; }
        public Guid? PlayerId { get; }
        public long? GraphRevision { get; }
        public int? CohortSize { get; }
    }
}
