using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;

namespace GamePlatform.Features.Progression
{

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
}
