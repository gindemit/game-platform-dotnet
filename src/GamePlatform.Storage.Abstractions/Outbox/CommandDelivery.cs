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
        public LeasedCommand(OperationId operationId, ClientStreamId streamId, long sequence, string operationKind, int schemaVersion, int fingerprintVersion, byte[] body, byte[] fingerprint)
        {
            if (!operationId.IsValid || !streamId.IsValid || sequence <= 0) throw new ArgumentException("Valid command identity is required.");
            if (string.IsNullOrWhiteSpace(operationKind) || schemaVersion<=0 || fingerprintVersion<=0 || body == null || body.Length == 0 || body.Length>262_144 || fingerprint == null || fingerprint.Length == 0 || fingerprint.Length>512) throw new ArgumentException("Complete command semantics are required.");
            OperationId=operationId;StreamId=streamId;Sequence=sequence;OperationKind=operationKind;SchemaVersion=schemaVersion;FingerprintVersion=fingerprintVersion;this.body=(byte[])body.Clone();this.fingerprint=(byte[])fingerprint.Clone();
        }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public long Sequence { get; }
        public string OperationKind { get; }
        public int SchemaVersion { get; }
        public int FingerprintVersion { get; }
        public byte[] CopyBody()=> (byte[])body.Clone();
        public byte[] CopyFingerprint()=> (byte[])fingerprint.Clone();
    }

    public interface ICommandDeliveryStore
    {
        Task<LeasedCommand?> LeaseNextAsync(long nowMilliseconds,long leaseMilliseconds,CancellationToken cancellationToken);
        Task FinalizeAsync(OperationId operationId,long sequence,CommandTerminalOutcome outcome,byte[]? terminalResult,CancellationToken cancellationToken);
        Task ReleaseAsync(OperationId operationId,long sequence,CancellationToken cancellationToken);
    }
}
