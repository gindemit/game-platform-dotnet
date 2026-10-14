using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    public sealed class WalletSpendRejection
    {
        public WalletSpendRejection(ScopedOwnerContext owner, OperationId operationId, string code)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            WalletValidation.Diagnostic(code, nameof(code));
            Owner = owner;
            OperationId = operationId;
            Code = code;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public string Code { get; }
    }
}
