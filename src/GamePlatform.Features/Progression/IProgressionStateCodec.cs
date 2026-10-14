using System;
using System.Collections.Generic;

namespace GamePlatform.Features.Progression
{

    /// <summary>Serialization/transport boundary. An adapter maps the frozen gameplay command and projection; it must not invent rewards.</summary>
    public interface IProgressionStateCodec
    {
        byte[] EncodeCompletionCommand(PendingProgressionCompletion completion);
        byte[] EncodePending(IReadOnlyList<PendingProgressionCompletion> pending);
        IReadOnlyList<PendingProgressionCompletion> DecodePending(long localRevision, ReadOnlySpan<byte> payload);
        byte[] EncodeConfirmed(ProgressionConfirmedProjection projection);
        ProgressionConfirmedProjection DecodeConfirmed(long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload);
    }
}
