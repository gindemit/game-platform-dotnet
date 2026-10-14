using System;

namespace GamePlatform.Storage.Abstractions.Recovery
{

    public sealed class CommandStreamRecoveryRecord
    {
        public CommandStreamRecoveryRecord(CommandStreamRecoveryRequest request, CommandStreamRecoveryPhase phase,
            long? remoteNextSequence, long? remotePriorFinalizedThrough)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            if (!Enum.IsDefined(typeof(CommandStreamRecoveryPhase), phase) ||
                (phase == CommandStreamRecoveryPhase.Prepared && (remoteNextSequence.HasValue || remotePriorFinalizedThrough.HasValue)) ||
                (phase >= CommandStreamRecoveryPhase.RemoteCommitted && (remoteNextSequence != 1 || remotePriorFinalizedThrough != request.ExpectedFinalizedThrough)) ||
                (phase < CommandStreamRecoveryPhase.RemoteCommitted && (remoteNextSequence.HasValue || remotePriorFinalizedThrough.HasValue)))
                throw new ArgumentException("Recovery phase evidence is inconsistent.");
            Phase = phase;
            RemoteNextSequence = remoteNextSequence;
            RemotePriorFinalizedThrough = remotePriorFinalizedThrough;
        }
        public CommandStreamRecoveryRequest Request { get; }
        public CommandStreamRecoveryPhase Phase { get; }
        public long? RemoteNextSequence { get; }
        public long? RemotePriorFinalizedThrough { get; }
    }
}
