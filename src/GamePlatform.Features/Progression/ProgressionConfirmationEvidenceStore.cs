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
    /// A non-empty group with only new operations may be appended at the latest revision; earlier groups never change.
    /// </summary>
    public interface IProgressionConfirmationEvidenceStore
    {
        Task<IReadOnlyList<ProgressionConfirmationEvidence>> ReadAsync(ScopedOwnerContext owner, CancellationToken cancellationToken);
        /// <summary>Verifies each recorded operation against the durable pending record in this exact transaction, then inserts immutable evidence.</summary>
        void ValidateAndInsert(ILocalStorageTransaction transaction, ScopedOwnerContext owner, ProgressionConfirmationEvidence evidence,
            IProgressionStateCodec codec);
    }

    /// <summary>Optional bounded adapter for durable, view-scoped operation and group markers.</summary>
    public interface ICompactingProgressionConfirmationEvidenceStore : IProgressionConfirmationEvidenceStore
    {
        Task EnsureNormalizedAsync(ScopedOwnerContext owner, CancellationToken cancellationToken);
        Task<ProgressionConfirmationEvidenceHead> ReadActiveAsync(ScopedOwnerContext owner,
            IReadOnlyList<OperationId> pendingOperationIds, CancellationToken cancellationToken);
        Task<bool> MatchesAsync(ScopedOwnerContext owner, ProgressionConfirmationEvidence evidence,
            CancellationToken cancellationToken);
    }

    public sealed class ProgressionConfirmationEvidenceHead
    {
        public ProgressionConfirmationEvidenceHead(long? latestRevision,
            IReadOnlyList<ProgressionConfirmationEvidence> activeGroups)
        {
            if (latestRevision < 0) throw new ArgumentOutOfRangeException(nameof(latestRevision));
            LatestRevision = latestRevision;
            ActiveGroups = activeGroups ?? throw new ArgumentNullException(nameof(activeGroups));
        }
        public long? LatestRevision { get; }
        public IReadOnlyList<ProgressionConfirmationEvidence> ActiveGroups { get; }
    }
}
