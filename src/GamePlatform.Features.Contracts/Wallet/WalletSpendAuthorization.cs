using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    /// <summary>Fresh backend authorization for one immutable intent. This is not a balance, grant, receipt or debit.</summary>
    public sealed class WalletSpendAuthorization
    {
        public WalletSpendAuthorization(ScopedOwnerContext owner, OperationId operationId, string businessSource, WalletSpendIntent intent)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            WalletValidation.BusinessSource(businessSource, nameof(businessSource));
            Owner = owner;
            OperationId = operationId;
            BusinessSource = businessSource;
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public string BusinessSource { get; }
        public WalletSpendIntent Intent { get; }
    }
}
