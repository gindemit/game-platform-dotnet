using System;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Outbox
{
    public sealed class CommandDraft
    {
        private readonly byte[] semanticBody;

        public CommandDraft(
            OwnerScope owner,
            OperationId operationId,
            ClientStreamId streamId,
            string operationKind,
            int schemaVersion,
            int fingerprintVersion,
            ReadOnlySpan<byte> semanticBody)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A valid owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("A valid operation ID is required.", nameof(operationId));
            if (!streamId.IsValid)
                throw new ArgumentException("A valid stream ID is required.", nameof(streamId));
            if (string.IsNullOrWhiteSpace(operationKind) || operationKind.Length > 128)
                throw new ArgumentOutOfRangeException(nameof(operationKind));
            if (schemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (fingerprintVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(fingerprintVersion));
            if (semanticBody.Length == 0 || semanticBody.Length > 262_144)
                throw new ArgumentOutOfRangeException(nameof(semanticBody));
            Owner = owner;
            OperationId = operationId;
            StreamId = streamId;
            OperationKind = operationKind;
            SchemaVersion = schemaVersion;
            FingerprintVersion = fingerprintVersion;
            this.semanticBody = semanticBody.ToArray();
        }

        public OwnerScope Owner { get; }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string OperationKind { get; }
        public int SchemaVersion { get; }
        public int FingerprintVersion { get; }
        public int SemanticBodyLength => semanticBody.Length;

        public void CopySemanticBodyTo(Span<byte> destination)
        {
            if (destination.Length < semanticBody.Length)
                throw new ArgumentException("The destination is too small.", nameof(destination));
            semanticBody.AsSpan().CopyTo(destination);
        }
    }
}
