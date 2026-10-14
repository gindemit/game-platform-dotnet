using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace GamePlatform.Wire.Contracts.Friends
{

    public sealed class FriendsRankingCountrySelection
    {
        public FriendsRankingCountrySelection(string? currentCountryCode, string? pendingCountryCode, long? pendingEffectiveAt)
        {
            FriendsRankingValidation.Country(currentCountryCode, true);
            FriendsRankingValidation.Country(pendingCountryCode, true);
            if ((pendingCountryCode == null) != (pendingEffectiveAt == null))
                throw new ArgumentException("Pending country and time must be present together.");
            if (pendingEffectiveAt.HasValue)
                FriendsRankingValidation.Time(pendingEffectiveAt.Value);
            CurrentCountryCode = currentCountryCode;
            PendingCountryCode = pendingCountryCode;
            PendingEffectiveAt = pendingEffectiveAt;
        }
        public string? CurrentCountryCode { get; }
        public string? PendingCountryCode { get; }
        public long? PendingEffectiveAt { get; }
    }
}
