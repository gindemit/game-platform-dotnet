#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Shared;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Features.Store
{
    /// <summary>
    /// Queues server-priced offer purchase intents; it never debits, grants or confirms value locally.
    /// </summary>
    public sealed class StoreService : IDisposable
    {
        private readonly SourcedCommandAdmitter admitter;
        private readonly IStorePurchaseCommandCodec codec;

        public StoreService(ScopedOwnerContext owner, ClientStreamId streamId, IDurableFeatureStateStore state, IAtomicCommandStore commands,
            IStorePurchaseCommandCodec codec, UuidV7Generator ids)
        {
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            admitter = new SourcedCommandAdmitter(owner, streamId, "store", state, commands, ids);
        }

        /// <summary>
        /// Durably queues one store.offer.purchase command for the purchase key.
        /// </summary>
        /// <remarks>
        /// Repeating a purchase key returns the original admission; a different offer for the same key is an identity conflict.
        /// </remarks>
        public Task<CommandAdmission> PurchaseAsync(string offerId, int offerVersion, Guid purchaseKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(offerId)) throw new ArgumentException("An offer ID is required.", nameof(offerId));
            if (offerVersion <= 0) throw new ArgumentOutOfRangeException(nameof(offerVersion));
            var key = purchaseKey.ToString("D");
            if (key[14] != '7' || !UuidIdentity.IsValid(purchaseKey)) throw new ArgumentException("The purchase key must be a UUIDv7.", nameof(purchaseKey));
            var body = codec.EncodePurchaseCommand(offerId, offerVersion, purchaseKey);
            return admitter.AdmitAsync("store.purchase:" + key, "store.offer.purchase", body, cancellationToken);
        }

        public void Dispose() => admitter.Dispose();
    }
}
