#nullable enable
using System;

namespace GamePlatform.Wire.Contracts
{
    public sealed class QuestClaimCommand : IPushPayload
    {
        public QuestClaimCommand(string questId, string occurrenceKey)
        {
            QuestId = questId ?? throw new ArgumentNullException(nameof(questId));
            OccurrenceKey = occurrenceKey ?? throw new ArgumentNullException(nameof(occurrenceKey));
        }

        public string QuestId { get; }
        public string OccurrenceKey { get; }
    }
}
