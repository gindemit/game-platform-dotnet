#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Contracts.Wallet;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;

namespace GamePlatform.Features.Wallet
{
    /// <summary>
    /// Narrow G3 cached confirmed-wallet projection. It has no catalog dependency because the frozen
    /// G3 wire projection contains only a semantic currency ID, signed balance, and revision.
    /// Installation is intentionally restricted to a caller-owned bootstrap/pull transaction.
    /// </summary>
    public sealed class G3WalletProjectionService
    {
        private const string FeatureNamespace = "wallet.g3.confirmed";
        private const byte ConfirmedKind = 1;
        private const byte RemovedFromViewKind = 2;
        private const byte ResetKind = 3;
        private readonly ScopedOwnerContext owner;
        private readonly StorageScope storageScope;
        private readonly IDurableFeatureStateStore state;
        private readonly IG3WalletProjectionCodec codec;

        public G3WalletProjectionService(ScopedOwnerContext owner, StorageScope storageScope,
            IDurableFeatureStateStore state, IG3WalletProjectionCodec codec)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!Matches(storageScope, owner.Owner)) throw new ArgumentException("The wallet storage scope does not match the captured owner.", nameof(storageScope));
            if (codec == null) throw new ArgumentNullException(nameof(codec));
            if (codec.SchemaVersion < 1 || codec.SchemaVersion > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(codec));
            this.owner = owner;
            this.storageScope = storageScope;
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.codec = codec;
        }

        /// <summary>Returns only a durable cached projection. An absent or explicitly removed projection is Missing.</summary>
        public async Task<FeatureSnapshot<G3ConfirmedWalletProjection>> ReadCachedAsync(G3WalletProjectionRequest request, CancellationToken cancellationToken)
        {
            var exact = EnsureRequest(request);
            var durable = await state.ReadAsync(exact.Owner, FeatureNamespace, EntityKey(exact.CurrencyId), cancellationToken).ConfigureAwait(false);
            if (durable == null) return Missing(exact.Owner);
            if (durable.ExtensionLength != 0) throw new InvalidOperationException("The G3 wallet projection has unsupported extensions.");

            var decoded = Decode(durable, exact.CurrencyId);
            if (decoded.Kind != ConfirmedKind) return Missing(exact.Owner);
            return new FeatureSnapshot<G3ConfirmedWalletProjection>(exact.Owner, decoded.Projection!.Revision,
                FeatureSnapshotState.Stale, SnapshotFreshness.Stale, decoded.Projection, null, null);
        }

        /// <summary>
        /// Writes one strictly newer confirmed record into the caller's existing bootstrap/pull transaction.
        /// A missing durable row is represented by prior revision -1, so server revision zero remains valid.
        /// </summary>
        public void ApplyConfirmed(ILocalStorageTransaction transaction, long durablePriorRevision,
            G3WalletProjectionRequest request, G3ConfirmedWalletProjection projection)
        {
            var exact = EnsureRequest(request);
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            if (!projection.CurrencyId.Equals(exact.CurrencyId)) throw new G3WalletProjectionConflictException("The confirmed currency does not match the requested semantic currency.");
            Apply(transaction, durablePriorRevision, exact, projection.Revision, ConfirmedKind, projection);
        }

        /// <summary>
        /// Persists an explicit private-feed removal/reset marker inside the caller's transaction. It reads as Missing,
        /// avoiding an invented zero balance while retaining the monotonic revision fence for a later reappearance.
        /// </summary>
        public void ApplyRemoval(ILocalStorageTransaction transaction, long durablePriorRevision,
            G3WalletProjectionRequest request, long revision, G3WalletProjectionRemoval removal)
        {
            var exact = EnsureRequest(request);
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!Enum.IsDefined(typeof(G3WalletProjectionRemoval), removal)) throw new ArgumentOutOfRangeException(nameof(removal));
            Apply(transaction, durablePriorRevision, exact, revision,
                removal == G3WalletProjectionRemoval.RemoveFromView ? RemovedFromViewKind : ResetKind, null);
        }

        private void Apply(ILocalStorageTransaction transaction, long durablePriorRevision, G3WalletProjectionRequest request,
            long revision, byte kind, G3ConfirmedWalletProjection? projection)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (!transaction.Scope.Equals(storageScope)) throw new G3WalletProjectionOwnerMismatchException();
            if (durablePriorRevision < -1) throw new ArgumentOutOfRangeException(nameof(durablePriorRevision));
            if (revision <= durablePriorRevision) throw new G3WalletProjectionConflictException("The G3 wallet projection revision must strictly advance.");

            var payload = Encode(kind, projection);
            state.Upsert(transaction, new DurableFeatureMutation(request.Owner, FeatureNamespace, EntityKey(request.CurrencyId),
                revision, 0, payload, Array.Empty<byte>()));
        }

        private byte[] Encode(byte kind, G3ConfirmedWalletProjection? projection)
        {
            byte[] body;
            if (kind == ConfirmedKind)
            {
                body = codec.Encode(projection!);
                if (body == null || body.Length == 0 || body.Length > 262_142) throw new InvalidOperationException("The G3 wallet projection codec payload is invalid.");
            }
            else body = Array.Empty<byte>();
            var payload = new byte[2 + body.Length];
            payload[0] = kind;
            payload[1] = checked((byte)codec.SchemaVersion);
            if (body.Length != 0) Buffer.BlockCopy(body, 0, payload, 2, body.Length);
            return payload;
        }

        private Decoded Decode(DurableFeatureState durable, SemanticId currencyId)
        {
            var payload = durable.CopyPayload();
            if (payload.Length < 2) throw new InvalidOperationException("The G3 wallet projection envelope is invalid.");
            var kind = payload[0];
            var schema = payload[1];
            if (schema == 0 || schema != codec.SchemaVersion) throw new InvalidOperationException("The G3 wallet projection schema is unsupported.");
            if (kind == ConfirmedKind)
            {
                if (payload.Length == 2) throw new InvalidOperationException("The G3 wallet confirmed projection payload is missing.");
                var projection = codec.Decode(schema, new ReadOnlySpan<byte>(payload, 2, payload.Length - 2));
                if (projection == null || !projection.CurrencyId.Equals(currencyId)) throw new InvalidOperationException("The G3 wallet codec returned an incompatible projection.");
                if (projection.Revision != durable.Revision) throw new InvalidOperationException("The G3 wallet durable revision does not match its projection.");
                return new Decoded(kind, projection);
            }
            if ((kind == RemovedFromViewKind || kind == ResetKind) && payload.Length == 2)
                return new Decoded(kind, null);
            throw new InvalidOperationException("The G3 wallet projection kind is invalid.");
        }

        private G3WalletProjectionRequest EnsureRequest(G3WalletProjectionRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Owner != owner) throw new G3WalletProjectionOwnerMismatchException();
            return request;
        }

        private static FeatureSnapshot<G3ConfirmedWalletProjection> Missing(ScopedOwnerContext owner) =>
            new FeatureSnapshot<G3ConfirmedWalletProjection>(owner, 0, FeatureSnapshotState.Missing, SnapshotFreshness.Missing, null, null, null);

        private static string EntityKey(SemanticId currencyId) => "currency/" + currencyId.Value;
        private static bool Matches(StorageScope scope, OwnerScope candidate) => string.Equals(scope.BackendNamespace, candidate.Backend.Value, StringComparison.Ordinal) &&
            string.Equals(scope.AppId.Value, candidate.AppId.ToString(), StringComparison.Ordinal) && string.Equals(scope.AccountId.Value, candidate.UserId.ToString(), StringComparison.Ordinal);

        private readonly struct Decoded
        {
            public Decoded(byte kind, G3ConfirmedWalletProjection? projection) { Kind = kind; Projection = projection; }
            public byte Kind { get; }
            public G3ConfirmedWalletProjection? Projection { get; }
        }
    }

    public sealed class G3WalletProjectionConflictException : InvalidOperationException
    {
        public G3WalletProjectionConflictException(string message) : base(message) { }
    }

    public sealed class G3WalletProjectionOwnerMismatchException : InvalidOperationException
    {
        public G3WalletProjectionOwnerMismatchException() : base("The G3 wallet projection does not belong to the captured account/view generation.") { }
    }
}
