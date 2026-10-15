using System;
using System.Collections.Generic;
namespace GamePlatform.Wire.Contracts.Daily
{
    public sealed class DailyClaimedResult : IPushAcceptedResult
    {
        public DailyClaimedResult(Guid claimId, Guid originatingOperationId, string utcDate, int cycleDay, int policyVersion, IReadOnlyList<ILiveSliceRewardLine> lines)
        {
            ClaimId = claimId;
            OriginatingOperationId = originatingOperationId;
            UtcDate = utcDate ?? throw new ArgumentNullException(nameof(utcDate));
            CycleDay = cycleDay;
            PolicyVersion = policyVersion;
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        }
        public Guid ClaimId { get; }
        public Guid OriginatingOperationId { get; }
        public string UtcDate { get; }
        public int CycleDay { get; }
        public int PolicyVersion { get; }
        public IReadOnlyList<ILiveSliceRewardLine> Lines { get; }
    }
}
