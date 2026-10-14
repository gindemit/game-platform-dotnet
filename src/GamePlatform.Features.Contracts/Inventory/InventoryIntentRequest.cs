using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{

    public sealed class InventoryIntentRequest
    {
        public InventoryIntentRequest(ScopedOwnerContext owner, OperationId operationId, ClientStreamId streamId, string businessSource,
            InventoryIntentKind kind, InventoryEntryKind targetKind, PlatformId targetId)
        {
            if (!owner.IsValid || !operationId.IsValid || !streamId.IsValid || !targetId.IsValid)
                throw new ArgumentException("Valid captured command identity is required.");
            if (!Enum.IsDefined(typeof(InventoryIntentKind), kind) || !Enum.IsDefined(typeof(InventoryEntryKind), targetKind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            InventoryValidation.BusinessSource(businessSource, nameof(businessSource));
            Owner = owner;
            OperationId = operationId;
            StreamId = streamId;
            BusinessSource = businessSource;
            Kind = kind;
            TargetKind = targetKind;
            TargetId = targetId;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public InventoryIntentKind Kind { get; }
        public InventoryEntryKind TargetKind { get; }
        public PlatformId TargetId { get; }
    }
}
