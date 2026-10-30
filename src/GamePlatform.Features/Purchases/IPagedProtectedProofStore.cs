using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Purchases
{
    /// <summary>Required for stores that grow beyond the legacy 256-record bound. Cursors bind owner, collection and high-water snapshot.</summary>
    public interface IPagedProtectedProofStore : IProtectedProofStore
    {
        Task<NativeProtectedPage<NativeAttempt>> ReadAttemptPageAsync(ScopedOwnerContext owner, string? cursor, int limit, CancellationToken token);
        Task<NativeProtectedPage<ProtectedNativeProofRecord>> ReadProofPageAsync(ScopedOwnerContext owner, string? cursor, int limit, CancellationToken token);
        Task<NativeAttempt?> FindAttemptAsync(ScopedOwnerContext owner, Guid intentKey, CancellationToken token);
    }
}
