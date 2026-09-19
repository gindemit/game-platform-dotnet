using System;
using System.Threading;
using System.Threading.Tasks;
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
            if (!owner.IsValid) throw new ArgumentException("A valid owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("A valid operation ID is required.", nameof(operationId));
            if (!streamId.IsValid) throw new ArgumentException("A valid stream ID is required.", nameof(streamId));
            if (string.IsNullOrWhiteSpace(operationKind) || operationKind.Length > 128)
                throw new ArgumentOutOfRangeException(nameof(operationKind));
            if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (fingerprintVersion <= 0) throw new ArgumentOutOfRangeException(nameof(fingerprintVersion));
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
            if (destination.Length < semanticBody.Length) throw new ArgumentException("The destination is too small.", nameof(destination));
            semanticBody.AsSpan().CopyTo(destination);
        }
    }

    public readonly struct CommandAdmission
    {
        public CommandAdmission(OperationId operationId, ClientStreamId streamId, long sequence, long localRevision)
        {
            if (!operationId.IsValid || !streamId.IsValid) throw new ArgumentException("Valid command identities are required.");
            if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (localRevision <= 0) throw new ArgumentOutOfRangeException(nameof(localRevision));
            OperationId = operationId;
            StreamId = streamId;
            Sequence = sequence;
            LocalRevision = localRevision;
        }

        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public long Sequence { get; }
        public long LocalRevision { get; }
    }

    public interface ICommandFingerprint
    {
        int GetFingerprintLength(int fingerprintVersion);
        void Compute(
            OwnerScope owner,
            OperationId operationId,
            ClientStreamId streamId,
            long sequence,
            string operationKind,
            int schemaVersion,
            int fingerprintVersion,
            ReadOnlySpan<byte> semanticBody,
            Span<byte> destination);
    }

    public interface IAtomicCommandStore
    {
        Task<CommandAdmission> CommitAsync(
            string businessRunId,
            CommandDraft command,
            Action<ILocalStorageTransaction, long> applyProjection,
            CancellationToken cancellationToken);
    }
}
