using System;
using System.Collections.Generic;
namespace GamePlatform.Features.Daily
{
    /// <summary>Server-owned claim status for the account's current UTC day.</summary>
    public sealed class DailyStatus
    {
        public DailyStatus(string utcDate, long nextUtcDayAtMilliseconds, bool eligible, int cycleDay, int policyVersion,
            IReadOnlyList<DailyPlannedLine> reward, IReadOnlyList<DailyClaimEntry> history)
        {
            if (nextUtcDayAtMilliseconds < 0 || nextUtcDayAtMilliseconds > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(nextUtcDayAtMilliseconds));
            if (reward == null)
                throw new ArgumentNullException(nameof(reward));
            if (reward.Count < 1 || reward.Count > 64)
                throw new ArgumentOutOfRangeException(nameof(reward));
            if (history == null)
                throw new ArgumentNullException(nameof(history));
            if (history.Count > 7)
                throw new ArgumentOutOfRangeException(nameof(history));
            UtcDate = DailyValidation.UtcDate(utcDate, nameof(utcDate));
            NextUtcDayAtMilliseconds = nextUtcDayAtMilliseconds;
            Eligible = eligible;
            CycleDay = DailyValidation.CycleDay(cycleDay, nameof(cycleDay));
            PolicyVersion = DailyValidation.PolicyVersion(policyVersion, nameof(policyVersion));
            Reward = reward;
            History = history;
        }
        public string UtcDate { get; }
        public long NextUtcDayAtMilliseconds { get; }
        public bool Eligible { get; }
        public int CycleDay { get; }
        public int PolicyVersion { get; }
        public IReadOnlyList<DailyPlannedLine> Reward { get; }
        public IReadOnlyList<DailyClaimEntry> History { get; }
    }
}
