#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;

namespace GamePlatform.Features.RewardFulfillment
{
    /// <summary>Consumer-owned conditional-insert seam for exactly-once receipt presentation. The callback must run in the same successful claim transaction.</summary>
    public interface IRewardPresentationClaimStore
    {
        Task<bool> TryClaimAsync(ScopedOwnerContext owner, Guid grantId, Action<ILocalStorageTransaction> onClaim, CancellationToken cancellationToken);
    }
}
