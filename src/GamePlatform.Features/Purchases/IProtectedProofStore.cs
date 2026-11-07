using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
namespace GamePlatform.Features.Purchases
{
    /// <summary>Host-provided durable authenticated encryption and atomic proof/attempt metadata store.</summary>
    public interface IProtectedProofStore
    {
        Task ReserveAttemptAsync(ScopedOwnerContext owner, Guid intentKey, NativeProduct product, int maximumProofBytes, CancellationToken token);
        Task CommitIntentAsync(ScopedOwnerContext owner, Guid intentKey, NativeIntentState intent, CancellationToken token);
        Task MarkLaunchRequestedAsync(ScopedOwnerContext owner, Guid intentKey, CancellationToken token);
        Task<IReadOnlyList<NativeAttempt>> ReadAttemptsAsync(ScopedOwnerContext owner, CancellationToken token);
        /// <summary>Atomically protects a proof and preserves its durable token identity on replay.</summary>
        /// <param name="owner">The captured owner of the proof and any trusted reservation.</param>
        /// <param name="proof">Private provider evidence; matching a SKU alone never establishes attempt ownership.</param>
        /// <param name="proposedRecoveryIntentKey">A candidate recovery key used only for a newly retained token.</param>
        /// <param name="reservedAttemptKey">An independently trusted attempt association, or null when none is supplied.
        /// Coordinator callback paths pass null. The returned record may retain a previously authenticated durable
        /// association on replay; the store must not infer an association from matching SKU or an active sheet.</param>
        /// <param name="token">Cancellation for this persistence operation.</param>
        /// <returns>The retained record with its original proof/recovery identities and trusted association, if any.</returns>
        Task<ProtectedNativeProofRecord> ProtectAndCommitAsync(ScopedOwnerContext owner, NativeProof proof, Guid proposedRecoveryIntentKey, Guid? reservedAttemptKey, CancellationToken token);
        Task<IReadOnlyList<ProtectedNativeProofRecord>> ReadProtectedRecordsAsync(ScopedOwnerContext owner, CancellationToken token);
        Task<NativeProof> OpenForSubmissionAsync(ScopedOwnerContext owner, Guid proofId, CancellationToken token);
        Task MarkFinalizedAsync(ScopedOwnerContext owner, Guid proofId, Guid chargeId, CancellationToken token);
        Task<bool> TryClaimPresentationAsync(ScopedOwnerContext owner, Guid grantId, CancellationToken token);
    }
}
