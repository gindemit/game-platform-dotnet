using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Progression
{
    /// <summary>Immutable, pull-derived operation evidence. An empty list is meaningful evidence for a replacement/reset projection.</summary>
    public sealed class ProgressionConfirmationEvidence
    {
        private readonly ReadOnlyCollection<OperationId> operationIds;
        public ProgressionConfirmationEvidence(long projectionRevision, IReadOnlyList<OperationId> operationIds)
        {
            if (projectionRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(projectionRevision));
            if (operationIds == null || operationIds.Count > 256 || operationIds.Any(value => !value.IsValid) || operationIds.Distinct().Count() != operationIds.Count)
                throw new ArgumentException("Confirmation operation identities must be valid, unique, and bounded.", nameof(operationIds));
            ProjectionRevision = projectionRevision;
            this.operationIds = new ReadOnlyCollection<OperationId>(operationIds.ToArray());
        }
        public long ProjectionRevision { get; }
        public IReadOnlyList<OperationId> OperationIds => operationIds;
    }
}
