using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions.Outbox;
namespace GamePlatform.Features.Store
{
    public sealed class StorePurchaseObserver : IDisposable
    {
        private readonly ScopedOwnerContext owner;
        private readonly IStorePurchaseRecordReader reader;
        private readonly IStorePurchaseObservationCodec codec;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool disposed;
        public StorePurchaseObserver(ScopedOwnerContext owner, IStorePurchaseRecordReader reader, IStorePurchaseObservationCodec codec)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            this.owner = owner;
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }
        public async Task<IReadOnlyList<StorePurchaseObservation>> ReadAsync(ScopedOwnerContext requestedOwner, CancellationToken token)
        {
            EnsureOwner(requestedOwner);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token);
            var rows = await reader.ReadAsync(owner, linked.Token).ConfigureAwait(false);
            EnsureOwner(requestedOwner);
            linked.Token.ThrowIfCancellationRequested();
            return Array.AsReadOnly(rows.Select(codec.Decode).ToArray());
        }
        public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); }
        private void EnsureOwner(ScopedOwnerContext value)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StorePurchaseObserver));
            if (value != owner)
                throw new ArgumentException("The purchase observer belongs to another owner.", nameof(value));
        }
    }
}
