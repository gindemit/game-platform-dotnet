using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public sealed class RemoteCommand
    {
        private readonly byte[] semanticBody; private readonly byte[] fingerprint;
        public RemoteCommand(OperationId operationId, ClientStreamId streamId, Guid installationId, long sequence, string operationKind, int schemaVersion, int fingerprintVersion, long clientCreatedAt, byte[] semanticBody, byte[] fingerprint)
        {
            if (!operationId.IsValid || !streamId.IsValid || !UuidIdentity.IsValid(installationId) || sequence <= 0)
                throw new ArgumentException("Valid command identity is required.");
            PlatformNumbers.UnixMilliseconds(clientCreatedAt);
            if (string.IsNullOrWhiteSpace(operationKind) || schemaVersion <= 0 || fingerprintVersion <= 0 || semanticBody == null || semanticBody.Length == 0 || fingerprint == null || fingerprint.Length == 0)
                throw new ArgumentException("Complete command semantics are required.");
            OperationId = operationId;
            StreamId = streamId;
            InstallationId = installationId;
            Sequence = sequence;
            OperationKind = operationKind;
            SchemaVersion = schemaVersion;
            FingerprintVersion = fingerprintVersion;
            ClientCreatedAt = clientCreatedAt;
            this.semanticBody = (byte[])semanticBody.Clone();
            this.fingerprint = (byte[])fingerprint.Clone();
        }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public Guid InstallationId { get; }
        public long Sequence { get; }
        public string OperationKind { get; }
        public int SchemaVersion { get; }
        public int FingerprintVersion { get; }
        public long ClientCreatedAt { get; }
        public byte[] SemanticBody => (byte[])semanticBody.Clone(); public byte[] Fingerprint => (byte[])fingerprint.Clone();
    }
}
