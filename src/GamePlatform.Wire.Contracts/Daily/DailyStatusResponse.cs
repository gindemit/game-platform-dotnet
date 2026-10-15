using System;
using System.Collections.Generic;
namespace GamePlatform.Wire.Contracts.Daily
{
    public sealed class DailyStatusResponse
    {
        public DailyStatusResponse(string utcDate, long nextUtcDayAt, bool eligible, int cycleDay, int policyVersion,
            IReadOnlyList<DailyPlannedLine> reward, IReadOnlyList<DailyHistoryEntry> history)
        {
            UtcDate = utcDate ?? throw new ArgumentNullException(nameof(utcDate));
            NextUtcDayAt = nextUtcDayAt;
            Eligible = eligible;
            CycleDay = cycleDay;
            PolicyVersion = policyVersion;
            Reward = reward ?? throw new ArgumentNullException(nameof(reward));
            History = history ?? throw new ArgumentNullException(nameof(history));
        }
        public string UtcDate { get; }
        public long NextUtcDayAt { get; }
        public bool Eligible { get; }
        public int CycleDay { get; }
        public int PolicyVersion { get; }
        public IReadOnlyList<DailyPlannedLine> Reward { get; }
        public IReadOnlyList<DailyHistoryEntry> History { get; }
    }
}
