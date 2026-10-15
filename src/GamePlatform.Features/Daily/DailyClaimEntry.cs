using System;
using System.Collections.Generic;
using GamePlatform.Core;
namespace GamePlatform.Features.Daily
{
    public sealed class DailyClaimEntry
    {
        public DailyClaimEntry(string utcDate, int cycleDay, Guid claimId, int policyVersion, IReadOnlyList<DailyReceiptLine> lines)
        {
            if (!UuidIdentity.IsValid(claimId, false))
                throw new ArgumentException("A UUIDv7 claim identity is required.", nameof(claimId));
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));
            if (lines.Count < 1 || lines.Count > 64)
                throw new ArgumentOutOfRangeException(nameof(lines));
            UtcDate = DailyValidation.UtcDate(utcDate, nameof(utcDate));
            CycleDay = DailyValidation.CycleDay(cycleDay, nameof(cycleDay));
            ClaimId = claimId;
            PolicyVersion = DailyValidation.PolicyVersion(policyVersion, nameof(policyVersion));
            Lines = lines;
        }
        public string UtcDate { get; }
        public int CycleDay { get; }
        public Guid ClaimId { get; }
        public int PolicyVersion { get; }
        public IReadOnlyList<DailyReceiptLine> Lines { get; }
    }
}
