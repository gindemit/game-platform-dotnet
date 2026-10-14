using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    public sealed class WalletSpendRequest
    {
        public WalletSpendRequest(ScopedOwnerContext owner, OperationId operationId, ClientStreamId streamId, string businessSource, WalletSpendIntent intent)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (!streamId.IsValid)
                throw new ArgumentException("A valid stream identity is required.", nameof(streamId));
            WalletValidation.BusinessSource(businessSource, nameof(businessSource));
            Owner = owner;
            OperationId = operationId;
            StreamId = streamId;
            BusinessSource = businessSource;
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public WalletSpendIntent Intent { get; }
    }
}
