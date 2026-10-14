using System;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Outbox
{

    public interface ICommandFingerprint
    {
        int GetFingerprintLength(int fingerprintVersion);
        void Compute(
            OwnerScope owner,
            OperationId operationId,
            ClientStreamId streamId,
            Guid installationId,
            long sequence,
            string operationKind,
            int schemaVersion,
            int fingerprintVersion,
            long clientCreatedAt,
            ReadOnlySpan<byte> semanticBody,
            Span<byte> destination);
    }
}
