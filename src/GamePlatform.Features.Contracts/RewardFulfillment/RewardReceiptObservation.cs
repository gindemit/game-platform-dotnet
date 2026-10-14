using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{

    /// <summary>Caller identity for an observation of a trusted receipt endpoint. The receipt can originate from an earlier idempotent operation.</summary>
    public sealed class RewardReceiptObservation
    {
        public RewardReceiptObservation(ScopedOwnerContext owner, OperationId operationId, RewardReceipt receipt)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("An operation identity is required.", nameof(operationId));
            Owner = owner;
            OperationId = operationId;
            Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public RewardReceipt Receipt { get; }
    }
}
