using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{

    public sealed class InventoryIntent
    {
        public InventoryIntent(OperationId operationId, ClientStreamId streamId, string businessSource, InventoryIntentKind kind,
            InventoryEntryKind targetKind, PlatformId targetId, long expectedInventoryRevision, long localRevision)
            : this(operationId, streamId, businessSource, kind, targetKind, targetId, expectedInventoryRevision, localRevision,
                InventoryIntentStatus.AwaitingReceipt, null)
        {
        }

        public InventoryIntent(OperationId operationId, ClientStreamId streamId, string businessSource, InventoryIntentKind kind,
            InventoryEntryKind targetKind, PlatformId targetId, long expectedInventoryRevision, long localRevision,
            InventoryIntentStatus status, long? acceptedInventoryRevision)
        {
            if (!operationId.IsValid || !streamId.IsValid || !targetId.IsValid)
                throw new ArgumentException("Valid command identities are required.");
            if (!Enum.IsDefined(typeof(InventoryIntentKind), kind) || !Enum.IsDefined(typeof(InventoryEntryKind), targetKind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            InventoryValidation.BusinessSource(businessSource, nameof(businessSource));
            if (expectedInventoryRevision < 0 || localRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(expectedInventoryRevision));
            if (!Enum.IsDefined(typeof(InventoryIntentStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (status == InventoryIntentStatus.AcceptedAwaitingPull && (!acceptedInventoryRevision.HasValue || acceptedInventoryRevision.Value <= expectedInventoryRevision))
                throw new ArgumentException("An accepted inventory intent requires a newer resulting revision.", nameof(acceptedInventoryRevision));
            if (status != InventoryIntentStatus.AcceptedAwaitingPull && acceptedInventoryRevision.HasValue)
                throw new ArgumentException("Only accepted inventory intents carry a resulting revision.", nameof(acceptedInventoryRevision));
            OperationId = operationId;
            StreamId = streamId;
            BusinessSource = businessSource;
            Kind = kind;
            TargetKind = targetKind;
            TargetId = targetId;
            ExpectedInventoryRevision = expectedInventoryRevision;
            LocalRevision = localRevision;
            Status = status;
            AcceptedInventoryRevision = acceptedInventoryRevision;
        }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public InventoryIntentKind Kind { get; }
        public InventoryEntryKind TargetKind { get; }
        public PlatformId TargetId { get; }
        public long ExpectedInventoryRevision { get; }
        public long LocalRevision { get; }
        public InventoryIntentStatus Status { get; }
        public long? AcceptedInventoryRevision { get; }
    }
}
