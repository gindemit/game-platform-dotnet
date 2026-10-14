using System;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Outbox
{

    public readonly struct CommandAdmission
    {
        public CommandAdmission(OperationId operationId, ClientStreamId streamId, Guid installationId, long clientCreatedAt, long sequence, long localRevision)
        {
            if (!operationId.IsValid || !streamId.IsValid)
                throw new ArgumentException("Valid command identities are required.");
            if (!UuidIdentity.IsValid(installationId))
                throw new ArgumentException("A valid installation identity is required.", nameof(installationId));
            PlatformNumbers.UnixMilliseconds(clientCreatedAt);
            if (sequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            if (localRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(localRevision));
            OperationId = operationId;
            StreamId = streamId;
            InstallationId = installationId;
            ClientCreatedAt = clientCreatedAt;
            Sequence = sequence;
            LocalRevision = localRevision;
        }

        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public Guid InstallationId { get; }
        public long ClientCreatedAt { get; }
        public long Sequence { get; }
        public long LocalRevision { get; }
    }
}
