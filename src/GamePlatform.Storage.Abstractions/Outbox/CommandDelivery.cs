using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Outbox
{
    public enum CommandTerminalOutcome { Accepted, Rejected }

    public sealed class LeasedCommand
    {
        private readonly byte[] body;
        private readonly byte[] fingerprint;
        public LeasedCommand(OperationId operationId, ClientStreamId streamId, Guid installationId, long sequence, string operationKind, int schemaVersion, int fingerprintVersion, long clientCreatedAt, byte[] body, byte[] fingerprint)
        {
            if (!operationId.IsValid || !streamId.IsValid || sequence <= 0) throw new ArgumentException("Valid command identity is required.");
            if (!UuidIdentity.IsValid(installationId)) throw new ArgumentException("A valid installation identity is required.", nameof(installationId));
            PlatformNumbers.UnixMilliseconds(clientCreatedAt);
            if (string.IsNullOrWhiteSpace(operationKind) || schemaVersion <= 0 || fingerprintVersion <= 0 || body == null || body.Length == 0 || body.Length > 262_144 || fingerprint == null || fingerprint.Length == 0 || fingerprint.Length > 512) throw new ArgumentException("Complete command semantics are required.");
            OperationId = operationId; StreamId = streamId; InstallationId = installationId; Sequence = sequence; OperationKind = operationKind; SchemaVersion = schemaVersion; FingerprintVersion = fingerprintVersion; ClientCreatedAt = clientCreatedAt; this.body = (byte[])body.Clone(); this.fingerprint = (byte[])fingerprint.Clone();
        }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public Guid InstallationId { get; }
        public long Sequence { get; }
        public string OperationKind { get; }
        public int SchemaVersion { get; }
        public int FingerprintVersion { get; }
        public long ClientCreatedAt { get; }
        public byte[] CopyBody() => (byte[])body.Clone();
        public byte[] CopyFingerprint() => (byte[])fingerprint.Clone();
    }

    public interface ICommandDeliveryStore
    {
        Task<LeasedCommand?> LeaseNextAsync(long nowMilliseconds, long leaseMilliseconds, CancellationToken cancellationToken);
        Task FinalizeAsync(OperationId operationId, long sequence, CommandTerminalOutcome outcome, byte[]? terminalResult, CancellationToken cancellationToken);
        Task ReleaseAsync(OperationId operationId, long sequence, CancellationToken cancellationToken);
    }
}
