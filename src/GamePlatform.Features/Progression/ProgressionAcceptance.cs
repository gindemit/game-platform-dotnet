using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;
using GamePlatform.Features.Contracts;

namespace GamePlatform.Features.Progression
{

    public sealed class ProgressionAcceptance
    {
        public ProgressionAcceptance(ScopedOwnerContext owner, OperationId operationId)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("A valid operation ID is required.", nameof(operationId));
            Owner = owner;
            OperationId = operationId;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
    }
}
