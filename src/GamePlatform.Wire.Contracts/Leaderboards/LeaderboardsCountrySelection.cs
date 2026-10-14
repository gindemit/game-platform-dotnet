using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace GamePlatform.Wire.Contracts.Leaderboards
{

    public sealed class LeaderboardsCountrySelection
    {
        public LeaderboardsCountrySelection(string? currentCountryCode, string? pendingCountryCode, long? pendingEffectiveAt)
        {
            LeaderboardsValidation.Country(currentCountryCode, true);
            LeaderboardsValidation.Country(pendingCountryCode, true);
            if ((pendingCountryCode == null) != (pendingEffectiveAt == null))
                throw new ArgumentException("Pending country and time must be present together.");
            if (pendingEffectiveAt.HasValue)
                LeaderboardsValidation.Time(pendingEffectiveAt.Value);
            CurrentCountryCode = currentCountryCode;
            PendingCountryCode = pendingCountryCode;
            PendingEffectiveAt = pendingEffectiveAt;
        }
        public string? CurrentCountryCode { get; }
        public string? PendingCountryCode { get; }
        public long? PendingEffectiveAt { get; }
    }
}
