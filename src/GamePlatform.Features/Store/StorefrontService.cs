using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;

namespace GamePlatform.Features.Store
{
    /// <summary>Scoped, UI-independent storefront reads. Availability is advisory.</summary>
    public sealed class StorefrontService : IDisposable
    {
        private readonly ScopedOwnerContext owner;
        private readonly IStorefrontRemote remote;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool disposed;

        public StorefrontService(ScopedOwnerContext owner, IStorefrontRemote remote)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            this.owner = owner;
            this.remote = remote ?? throw new ArgumentNullException(nameof(remote));
        }

        public async Task<RemoteResult<IReadOnlyList<StorefrontOffer>>> FetchAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
            var result = await remote.FetchAsync(linked.Token).ConfigureAwait(false);
            EnsureOwner(requestedOwner);
            linked.Token.ThrowIfCancellationRequested();
            return result;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            lifetime.Cancel();
        }

        private void EnsureOwner(ScopedOwnerContext requestedOwner)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StorefrontService));
            if (owner != requestedOwner)
                throw new ArgumentException("The storefront belongs to another owner.", nameof(requestedOwner));
        }
    }
}
