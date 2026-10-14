using System;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public sealed class CommandStreamRecoveryResult
    {
        public CommandStreamRecoveryResult(ClientStreamId priorStreamId, ClientStreamId newStreamId,
            Guid recoveryId, long priorFinalizedThrough, long nextSequence, long serverTime)
        {
            if (!priorStreamId.IsValid || !newStreamId.IsValid || priorStreamId == newStreamId || !UuidIdentity.IsValid(recoveryId))
                throw new ArgumentException("Valid recovery identities are required.");
            if (priorFinalizedThrough < 0 || nextSequence != 1)
                throw new ArgumentOutOfRangeException(nameof(nextSequence));
            PlatformNumbers.UnixMilliseconds(serverTime);
            PriorStreamId = priorStreamId;
            NewStreamId = newStreamId;
            RecoveryId = recoveryId;
            PriorFinalizedThrough = priorFinalizedThrough;
            NextSequence = nextSequence;
            ServerTime = serverTime;
        }

        public ClientStreamId PriorStreamId { get; }
        public ClientStreamId NewStreamId { get; }
        public Guid RecoveryId { get; }
        public long PriorFinalizedThrough { get; }
        public long NextSequence { get; }
        public long ServerTime { get; }
    }
}
