#nullable enable
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;

namespace GamePlatform.Transport.Http
{
    /// <summary>
    /// Externally owned single-flight coordinator for one auth session/account
    /// scope. Providers borrow it and never dispose it.
    /// </summary>
    public sealed class AuthRefreshCoordinator : IDisposable
    {
        private readonly ConcurrentDictionary<RefreshKey, SemaphoreSlim> gates = new ConcurrentDictionary<RefreshKey, SemaphoreSlim>();
        private int disposed;

        public async Task<AccessTokenSnapshot> RefreshAsync(
            IAuthSession auth,
            PlatformUserId? accountId,
            long rejectedGeneration,
            CancellationToken cancellationToken)
        {
            if (auth == null) throw new ArgumentNullException(nameof(auth));
            if (string.IsNullOrWhiteSpace(auth.SessionKey) || auth.SessionKey.Length > 256) throw new ArgumentException("The auth session key is invalid.", nameof(auth));
            ThrowIfDisposed();
            var key = new RefreshKey(auth.SessionKey, accountId);
            var gate = gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                var current = await auth.GetAsync(cancellationToken).ConfigureAwait(false);
                return current.Generation == rejectedGeneration
                    ? await auth.RefreshAsync(rejectedGeneration, cancellationToken).ConfigureAwait(false)
                    : current;
            }
            finally { gate.Release(); }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            foreach (var gate in gates.Values) gate.Dispose();
            gates.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(AuthRefreshCoordinator));
        }

        private readonly struct RefreshKey : IEquatable<RefreshKey>
        {
            public RefreshKey(string sessionKey, PlatformUserId? accountId) { SessionKey = sessionKey; AccountId = accountId; }
            private string SessionKey { get; }
            private PlatformUserId? AccountId { get; }
            public bool Equals(RefreshKey other) => string.Equals(SessionKey, other.SessionKey, StringComparison.Ordinal) && Nullable.Equals(AccountId, other.AccountId);
            public override bool Equals(object? obj) => obj is RefreshKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked { return (StringComparer.Ordinal.GetHashCode(SessionKey) * 397) ^ (AccountId?.GetHashCode() ?? 0); }
            }
        }
    }
}
