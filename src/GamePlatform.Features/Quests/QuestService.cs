#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Shared;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Features.Quests
{
    /// <summary>
    /// Queues quest occurrence claims; it never evaluates objectives or grants rewards locally.
    /// </summary>
    public sealed class QuestService : IDisposable
    {
        private readonly SourcedCommandAdmitter admitter;
        private readonly IQuestClaimCommandCodec codec;

        public QuestService(ScopedOwnerContext owner, ClientStreamId streamId, IDurableFeatureStateStore state, IAtomicCommandStore commands,
            IQuestClaimCommandCodec codec, UuidV7Generator ids)
        {
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            admitter = new SourcedCommandAdmitter(owner, streamId, "quests", state, commands, ids);
        }

        /// <summary>
        /// Durably queues one quest.claim command for the occurrence; repeating it returns the original admission.
        /// </summary>
        public Task<CommandAdmission> ClaimAsync(string questId, string occurrenceKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(questId)) throw new ArgumentException("A quest ID is required.", nameof(questId));
            if (string.IsNullOrWhiteSpace(occurrenceKey) || occurrenceKey.Length > 128) throw new ArgumentException("An occurrence key is required.", nameof(occurrenceKey));
            var body = codec.EncodeClaimCommand(questId, occurrenceKey);
            return admitter.AdmitAsync("quest.claim:" + occurrenceKey, "quest.claim", body, cancellationToken);
        }

        public void Dispose() => admitter.Dispose();
    }
}
