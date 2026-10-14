using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    /// <summary>A receipt only records the expected future server projection revision; it never includes a locally trusted balance.</summary>
    public sealed class WalletSpendAcceptance
    {
        public WalletSpendAcceptance(ScopedOwnerContext owner, OperationId operationId, long resultingRevision)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (resultingRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(resultingRevision));
            Owner = owner;
            OperationId = operationId;
            ResultingRevision = resultingRevision;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public long ResultingRevision { get; }
    }
}
