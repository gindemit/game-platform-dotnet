using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{

    /// <summary>Receipt-derived outcome. It may only move a matching local command to awaiting-pull.</summary>
    public sealed class ProfileUpdateAcceptance
    {
        public ProfileUpdateAcceptance(ScopedOwnerContext owner, OperationId operationId, long resultingRevision)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (resultingRevision < 1)
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
