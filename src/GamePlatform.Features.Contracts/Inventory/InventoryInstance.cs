using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{

    public sealed class InventoryInstance
    {
        private const int MaximumPayloadBytes = 16 * 1024;
        private const int MaximumAuditBytes = 16 * 1024;
        private readonly byte[] payload;
        private readonly byte[] auditExtensions;

        public InventoryInstance(PlatformId instanceId, PlatformId definitionId, long revision,
            InventoryInstanceState state, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> auditExtensions)
        {
            if (!instanceId.IsValid || !definitionId.IsValid)
                throw new ArgumentException("Instance and definition IDs are required.");
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            if (!Enum.IsDefined(typeof(InventoryInstanceState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            if (payload.Length > MaximumPayloadBytes || auditExtensions.Length > MaximumAuditBytes)
                throw new ArgumentOutOfRangeException(nameof(payload));
            if (state == InventoryInstanceState.Tombstoned && payload.Length != 0)
                throw new ArgumentException("A tombstone carries no instance payload.", nameof(payload));
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Revision = revision;
            State = state;
            this.payload = payload.ToArray();
            this.auditExtensions = auditExtensions.ToArray();
        }
        public PlatformId InstanceId { get; }
        public PlatformId DefinitionId { get; }
        public long Revision { get; }
        public InventoryInstanceState State { get; }
        public byte[] CopyPayload() => (byte[])payload.Clone();
        public byte[] CopyAuditExtensions() => (byte[])auditExtensions.Clone();
    }
}
