#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;

namespace GamePlatform.Features.Progression
{
    /// <summary>Immutable, pull-derived operation evidence. An empty list is meaningful evidence for a replacement/reset projection.</summary>
    public sealed class ProgressionConfirmationEvidence
    {
        private readonly ReadOnlyCollection<OperationId> operationIds;
        public ProgressionConfirmationEvidence(long projectionRevision, IReadOnlyList<OperationId> operationIds)
        {
            if (projectionRevision < 0) throw new ArgumentOutOfRangeException(nameof(projectionRevision));
            if (operationIds == null || operationIds.Count > 256 || operationIds.Any(value => !value.IsValid) || operationIds.Distinct().Count() != operationIds.Count)
                throw new ArgumentException("Confirmation operation identities must be valid, unique, and bounded.", nameof(operationIds));
            ProjectionRevision = projectionRevision; this.operationIds = new ReadOnlyCollection<OperationId>(operationIds.ToArray());
        }
        public long ProjectionRevision { get; }
        public IReadOnlyList<OperationId> OperationIds => operationIds;
    }

    /// <summary>
    /// Consumer-owned evidence seam for a private-feed group. Implementations insert the exact group once or verify
    /// the immutable prior group in the same transaction; they must reject an operation ID under a different revision.
    /// </summary>
    public interface IProgressionConfirmationEvidenceStore
    {
        Task<IReadOnlyList<ProgressionConfirmationEvidence>> ReadAsync(ScopedOwnerContext owner, CancellationToken cancellationToken);
        /// <summary>Verifies each recorded operation against the durable pending record in this exact transaction, then inserts immutable evidence.</summary>
        void ValidateAndInsert(ILocalStorageTransaction transaction, ScopedOwnerContext owner, ProgressionConfirmationEvidence evidence,
            IProgressionStateCodec codec);
    }
}
