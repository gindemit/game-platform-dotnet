#nullable enable
using System;
using System.Collections.Generic;

namespace GamePlatform.Wire.Contracts
{
    public sealed class QuestClaimedResult : IPushAcceptedResult
    {
        public QuestClaimedResult(Guid claimId, Guid originatingOperationId, string questId, string occurrenceKey, IReadOnlyList<ILiveSliceRewardLine> lines)
        {
            ClaimId = claimId;
            OriginatingOperationId = originatingOperationId;
            QuestId = questId ?? throw new ArgumentNullException(nameof(questId));
            OccurrenceKey = occurrenceKey ?? throw new ArgumentNullException(nameof(occurrenceKey));
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        }

        public Guid ClaimId { get; }
        public Guid OriginatingOperationId { get; }
        public string QuestId { get; }
        public string OccurrenceKey { get; }
        public IReadOnlyList<ILiveSliceRewardLine> Lines { get; }
    }
}
