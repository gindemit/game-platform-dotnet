using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{

    public sealed class RewardPresentationQuery
    {
        public RewardPresentationQuery(ScopedOwnerContext owner, OperationId operationId)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("An operation identity is required.", nameof(operationId));
            Owner = owner;
            OperationId = operationId;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
    }
}
