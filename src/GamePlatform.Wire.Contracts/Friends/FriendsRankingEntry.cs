using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace GamePlatform.Wire.Contracts.Friends
{
    public sealed class FriendsRankingEntry
    {
        public FriendsRankingEntry(Guid participantId, long rank, long score, string displayName, string? avatarKey = null,
            string? badgeId = null, string? countryCode = null)
        {
            if (participantId == Guid.Empty || rank < 1 || rank > 500 || score < 0)
                throw new ArgumentOutOfRangeException(nameof(rank));
            FriendsRankingValidation.Text(displayName, 64, nameof(displayName));
            if (avatarKey != null)
                FriendsRankingValidation.Text(avatarKey, 128, nameof(avatarKey), true);
            if (badgeId != null)
                FriendsRankingValidation.Text(badgeId, 48, nameof(badgeId), true);
            FriendsRankingValidation.Country(countryCode, true);
            ParticipantId = participantId;
            Rank = rank;
            Score = score;
            DisplayName = displayName;
            AvatarKey = avatarKey;
            BadgeId = badgeId;
            CountryCode = countryCode;
        }
        public Guid ParticipantId { get; }
        public long Rank { get; }
        public long Score { get; }
        public string DisplayName { get; }
        public string? AvatarKey { get; }
        public string? BadgeId { get; }
        public string? CountryCode { get; }
    }
}
