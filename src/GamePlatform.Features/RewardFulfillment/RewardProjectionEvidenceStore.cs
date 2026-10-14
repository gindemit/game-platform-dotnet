#nullable enable
using System;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;

namespace GamePlatform.Features.RewardFulfillment
{
    /// <summary>Consumer-owned transaction seam for immutable receipt-group evidence. It must insert once or verify exact existing bytes; it must never replace equal-revision evidence.</summary>
    public interface IRewardProjectionEvidenceStore
    {
        void InsertOrVerify(ILocalStorageTransaction transaction, ScopedOwnerContext owner, Guid grantId,
            long feedRevision, long recordedAtMilliseconds, byte[] canonicalPayload);
    }
}
