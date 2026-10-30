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
        Task<ProtectedNativeProofRecord> ProtectAndCommitAsync(ScopedOwnerContext owner, NativeProof proof, Guid proposedRecoveryIntentKey, Guid? reservedAttemptKey, CancellationToken token);
        Task<IReadOnlyList<ProtectedNativeProofRecord>> ReadProtectedRecordsAsync(ScopedOwnerContext owner, CancellationToken token);
        Task<NativeProof> OpenForSubmissionAsync(ScopedOwnerContext owner, Guid proofId, CancellationToken token);
        Task MarkFinalizedAsync(ScopedOwnerContext owner, Guid proofId, Guid chargeId, CancellationToken token);
        Task<bool> TryClaimPresentationAsync(ScopedOwnerContext owner, Guid grantId, CancellationToken token);
    }
}
