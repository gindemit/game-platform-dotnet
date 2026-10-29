using System;
using GamePlatform.Core;
namespace GamePlatform.Storage.Abstractions.Outbox
{
    public sealed class StorePurchaseRecord
    {
        private readonly byte[] body;
        private readonly byte[]? terminal;
        public StorePurchaseRecord(OperationId operationId, long sequence, string deliveryState, byte[] body,
            byte[]? terminal, bool compatibleCheckpoint, long committedThrough)
        {
            OperationId = operationId;
            Sequence = sequence;
            DeliveryState = deliveryState;
            this.body = (byte[])body.Clone();
            this.terminal = terminal == null ? null : (byte[])terminal.Clone();
            CompatibleCheckpoint = compatibleCheckpoint;
            CommittedThrough = committedThrough;
        }
        public OperationId OperationId { get; }
        public long Sequence { get; }
        public string DeliveryState { get; }
        public byte[] CopyBody() => (byte[])body.Clone();
        public byte[]? CopyTerminal() => terminal == null ? null : (byte[])terminal.Clone();
        public bool CompatibleCheckpoint { get; }
        public long CommittedThrough { get; }
    }
}
