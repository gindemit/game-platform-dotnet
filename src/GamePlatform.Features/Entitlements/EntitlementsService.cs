#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Catalog;
using GamePlatform.Features.Contracts.Entitlements;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;

namespace GamePlatform.Features.Entitlements
{
    /// <summary>Portable, server-confirmed entitlement projection. It has no receipt verifier, grant operation, or device clock.</summary>
    public sealed class EntitlementsService : IDisposable
    {
        private const string Namespace = "entitlements";
        private const string ConfirmedKey = "confirmed";
        private readonly ScopedOwnerContext owner;
        private readonly StorageScope scope;
        private readonly IDurableFeatureStateStore state;
        private readonly ISerializedStorageExecutor transactions;
        private readonly IEntitlementStateCodec codec;
        private readonly SemaphoreSlim mutation = new SemaphoreSlim(1, 1);
        private bool disposed;

        public EntitlementsService(ScopedOwnerContext owner, StorageScope scope, IDurableFeatureStateStore state,
            ISerializedStorageExecutor transactions, IEntitlementStateCodec codec)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!Matches(scope, owner.Owner)) throw new ArgumentException("The entitlement scope does not match the captured owner.", nameof(scope));
            this.owner = owner;
            this.scope = scope;
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        /// <summary>Returns cached rights with explicit expiry uncertainty; device time never changes a right's authority.</summary>
        public Task<FeatureSnapshot<EntitlementSnapshot>> ReadCachedAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken) =>
            ReadSnapshotAsync(EnsureOwner(requestedOwner), cancellationToken);

        /// <summary>Installs an entire server-confirmed private transaction-group projection after exact app visibility validation.</summary>
        public async Task ApplyConfirmedAsync(ScopedOwnerContext requestedOwner, EntitlementConfirmedSnapshot confirmed,
            CatalogSnapshot catalog, CancellationToken cancellationToken)
        {
            var exactOwner = EnsureOwner(requestedOwner);
            if (confirmed == null) throw new ArgumentNullException(nameof(confirmed));
            ValidateCatalog(exactOwner, confirmed, catalog);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadConfirmedAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                if (current != null && confirmed.Revision < current.Revision) return;
                if (current != null && confirmed.Revision == current.Revision)
                {
                    if (!Equivalent(current, confirmed)) throw new EntitlementProjectionConflictException("A confirmed entitlement revision changed meaning.");
                    return;
                }

                await transactions.ExecuteAsync(scope, transaction =>
                {
                    ApplyConfirmedInTransaction(transaction, exactOwner, current?.Revision ?? 0, confirmed, catalog);
                    return true;
                }, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
        }

        /// <summary>
        /// Adds this projection to an already-open caller-owned transaction, so a private-feed adapter can commit it with
        /// its cursor and sibling effects. The caller supplies the projection revision read from that same group transaction;
        /// it must advance before this service can mutate durable state.
        /// </summary>
        public void ApplyConfirmedInTransaction(ILocalStorageTransaction transaction, ScopedOwnerContext requestedOwner,
            long durablePriorRevision, EntitlementConfirmedSnapshot confirmed, CatalogSnapshot catalog)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            var exactOwner = EnsureOwner(requestedOwner);
            if (!transaction.Scope.Equals(scope)) throw new EntitlementOwnerMismatchException();
            if (durablePriorRevision < 0) throw new ArgumentOutOfRangeException(nameof(durablePriorRevision));
            if (confirmed == null) throw new ArgumentNullException(nameof(confirmed));
            if (confirmed.Revision <= durablePriorRevision)
                throw new EntitlementProjectionConflictException("A borrowed entitlement projection must advance its durable prior revision.");
            ValidateCatalog(exactOwner, confirmed, catalog);
            var payload = codec.EncodeConfirmed(confirmed);
            // An empty byte payload is the valid canonical encoding of a server-confirmed empty app-visible projection.
            if (payload == null || payload.Length > 262_144) throw new EntitlementProjectionConflictException("The entitlement codec produced an invalid projection payload.");
            state.Upsert(transaction, new DurableFeatureMutation(exactOwner, Namespace, ConfirmedKey, confirmed.Revision,
                confirmed.ConfirmedAtMilliseconds, payload, Array.Empty<byte>()));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            mutation.Dispose();
        }

        private async Task<FeatureSnapshot<EntitlementSnapshot>> ReadSnapshotAsync(ScopedOwnerContext exactOwner, CancellationToken cancellationToken)
        {
            var confirmed = await ReadConfirmedAsync(exactOwner, cancellationToken).ConfigureAwait(false);
            if (confirmed == null)
                return new FeatureSnapshot<EntitlementSnapshot>(exactOwner, 0, FeatureSnapshotState.Missing, SnapshotFreshness.Missing, null, null, null);
            var views = confirmed.Rights.Select(right => new EntitlementRightView(right, OfflineEligibility(right))).ToArray();
            return new FeatureSnapshot<EntitlementSnapshot>(exactOwner, confirmed.Revision, FeatureSnapshotState.Stale,
                SnapshotFreshness.Stale, new EntitlementSnapshot(confirmed, views), confirmed.ConfirmedAtMilliseconds, null);
        }

        private async Task<EntitlementConfirmedSnapshot?> ReadConfirmedAsync(ScopedOwnerContext exactOwner, CancellationToken cancellationToken)
        {
            var stored = await state.ReadAsync(exactOwner, Namespace, ConfirmedKey, cancellationToken).ConfigureAwait(false);
            if (stored == null) return null;
            codec.ValidateSupportedExtensions(stored.CopyExtensions());
            var decoded = codec.DecodeConfirmed(stored.Revision, stored.ConfirmedAtMilliseconds, stored.CopyPayload(), stored.CopyExtensions());
            if (decoded == null || decoded.Revision != stored.Revision || decoded.ConfirmedAtMilliseconds != stored.ConfirmedAtMilliseconds)
                throw new EntitlementProjectionConflictException("The entitlement codec returned an invalid confirmed projection.");
            return decoded;
        }

        private ScopedOwnerContext EnsureOwner(ScopedOwnerContext requested)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EntitlementsService));
            if (requested != owner) throw new EntitlementOwnerMismatchException();
            return requested;
        }

        private static EntitlementOfflineEligibility OfflineEligibility(EntitlementRight right)
        {
            switch (right.State)
            {
                case EntitlementRightState.Active:
                    return right.ExpiresAtServerMilliseconds.HasValue ? EntitlementOfflineEligibility.ExpiryUncertain : EntitlementOfflineEligibility.Eligible;
                case EntitlementRightState.Revoked:
                case EntitlementRightState.Expired:
                    return EntitlementOfflineEligibility.Ineligible;
                default:
                    return EntitlementOfflineEligibility.Unknown;
            }
        }

        private static bool Matches(StorageScope scope, OwnerScope owner) =>
            string.Equals(scope.BackendNamespace, owner.Backend.Value, StringComparison.Ordinal) &&
            string.Equals(scope.AppId.Value, owner.AppId.ToString(), StringComparison.Ordinal) &&
            string.Equals(scope.AccountId.Value, owner.UserId.ToString(), StringComparison.Ordinal);

        private static void ValidateCatalog(ScopedOwnerContext owner, EntitlementConfirmedSnapshot confirmed, CatalogSnapshot catalog)
        {
            if (catalog == null || catalog.Request.Owner != owner) throw new EntitlementOwnerMismatchException();
            foreach (var right in confirmed.Rights)
            {
                var entry = catalog.Entries.SingleOrDefault(value => value.Definition.DefinitionId.Equals(right.EntitlementId));
                if (entry == null || entry.Definition.Kind != CatalogResourceKind.Entitlement || !entry.Binding.Permits(CatalogResourceKind.Entitlement, CatalogPermission.Visible))
                    throw new EntitlementProjectionConflictException("An entitlement is unavailable in the exact app-visible catalog.");
            }
        }

        private static bool Equivalent(EntitlementConfirmedSnapshot left, EntitlementConfirmedSnapshot right)
        {
            if (left.ConfirmedAtMilliseconds != right.ConfirmedAtMilliseconds || left.Rights.Count != right.Rights.Count) return false;
            var byId = right.Rights.ToDictionary(value => value.EntitlementId);
            foreach (var a in left.Rights)
            {
                if (!byId.TryGetValue(a.EntitlementId, out var b) || a.Revision != b.Revision || a.State != b.State ||
                    a.ExpiresAtServerMilliseconds != b.ExpiresAtServerMilliseconds || !a.OriginReceipt.ReceiptId.Equals(b.OriginReceipt.ReceiptId) ||
                    !string.Equals(a.OriginReceipt.BusinessSource, b.OriginReceipt.BusinessSource, StringComparison.Ordinal) || !EqualBytes(a.CopyPayload(), b.CopyPayload())) return false;
            }
            return true;
        }

        private static bool EqualBytes(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }
}
