using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Progression
{

    /// <summary>Pull-derived acknowledgement. Only explicitly listed, already accepted operations may leave the pending projection.</summary>
    public sealed class ProgressionConfirmation
    {
        private readonly ReadOnlyCollection<OperationId> confirmedOperationIds;
        public ProgressionConfirmation(ScopedOwnerContext owner, ProgressionConfirmedProjection projection, IReadOnlyList<OperationId> confirmedOperationIds)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            Projection = projection ?? throw new ArgumentNullException(nameof(projection));
            if (confirmedOperationIds == null || confirmedOperationIds.Any(value => !value.IsValid) || confirmedOperationIds.Distinct().Count() != confirmedOperationIds.Count)
                throw new ArgumentException("Confirmed operation identities must be valid and unique.", nameof(confirmedOperationIds));
            Owner = owner;
            this.confirmedOperationIds = new ReadOnlyCollection<OperationId>(confirmedOperationIds.ToArray());
        }
        public ScopedOwnerContext Owner { get; }
        public ProgressionConfirmedProjection Projection { get; }
        public IReadOnlyList<OperationId> ConfirmedOperationIds => confirmedOperationIds;
    }
}
