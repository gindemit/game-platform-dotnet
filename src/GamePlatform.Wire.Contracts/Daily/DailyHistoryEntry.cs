using System;
using System.Collections.Generic;
namespace GamePlatform.Wire.Contracts.Daily
{
    public sealed class DailyHistoryEntry
    {
        public DailyHistoryEntry(string utcDate, int cycleDay, Guid claimId, int policyVersion, IReadOnlyList<ILiveSliceRewardLine> lines)
        {
            UtcDate = utcDate ?? throw new ArgumentNullException(nameof(utcDate));
            CycleDay = cycleDay;
            ClaimId = claimId;
            PolicyVersion = policyVersion;
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        }
        public string UtcDate { get; }
        public int CycleDay { get; }
        public Guid ClaimId { get; }
        public int PolicyVersion { get; }
        public IReadOnlyList<ILiveSliceRewardLine> Lines { get; }
    }
}
