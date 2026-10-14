using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Progression
{

    /// <summary>Optional bounded adapter for durable, view-scoped operation and group markers.</summary>
    public interface ICompactingProgressionConfirmationEvidenceStore : IProgressionConfirmationEvidenceStore
    {
        Task EnsureNormalizedAsync(ScopedOwnerContext owner, CancellationToken cancellationToken);
        Task<ProgressionConfirmationEvidenceHead> ReadActiveAsync(ScopedOwnerContext owner,
            IReadOnlyList<OperationId> pendingOperationIds, CancellationToken cancellationToken);
        Task<bool> MatchesAsync(ScopedOwnerContext owner, ProgressionConfirmationEvidence evidence,
            CancellationToken cancellationToken);
    }
}
