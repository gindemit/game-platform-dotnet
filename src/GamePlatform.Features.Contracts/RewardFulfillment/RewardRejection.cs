using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.RewardFulfillment
{

    public sealed class RewardRejection
    {
        public RewardRejection(ScopedOwnerContext owner, OperationId operationId, string businessSource, string code)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("An operation identity is required.", nameof(operationId));
            RewardValidation.BusinessSource(businessSource, nameof(businessSource));
            RewardValidation.Text(code, 128, nameof(code));
            Owner = owner;
            OperationId = operationId;
            BusinessSource = businessSource;
            Code = code;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public string BusinessSource { get; }
        public string Code { get; }
    }
}
