#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Contracts.Wallet;
using GamePlatform.Features.Shared;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Features.Wallet
{
    /// <summary>Scoped confirmed-wallet projection and spend-intent coordinator. It has no local credit or offline-spend authority.</summary>
    public sealed class WalletService : IDisposable
    {
        private const string FeatureNamespace = "wallet";
        private readonly ScopedOwnerContext owner;
        private readonly StorageScope storageScope;
        private readonly IDurableFeatureStateStore state;
        private readonly ISerializedStorageExecutor transactions;
        private readonly IAtomicCommandStore commands;
        private readonly IWalletStateCodec codec;
        private readonly IWalletRemote remote;
        private readonly DisposableQueryCache<WalletRemoteRead> reads;
        private readonly Func<long> nowMilliseconds;
        private readonly SemaphoreSlim mutation = new SemaphoreSlim(1, 1);
        private bool disposed;

        public WalletService(ScopedOwnerContext owner, StorageScope storageScope, IDurableFeatureStateStore state,
            ISerializedStorageExecutor transactions, IAtomicCommandStore commands, IWalletStateCodec codec,
            IWalletRemote remote, int refreshCacheCapacity, TimeSpan refreshCacheLifetime, Func<long> nowMilliseconds)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!Matches(storageScope, owner.Owner)) throw new ArgumentException("The wallet storage scope does not match the captured owner.", nameof(storageScope));
            this.owner = owner; this.storageScope = storageScope;
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.remote = remote ?? throw new ArgumentNullException(nameof(remote));
            this.nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
            reads = new DisposableQueryCache<WalletRemoteRead>(refreshCacheCapacity, refreshCacheLifetime, this.nowMilliseconds);
        }

        public Task<FeatureSnapshot<WalletSnapshot>> ReadCachedAsync(WalletBalanceRequest request, CancellationToken cancellationToken) =>
            ReadSnapshotAsync(EnsureRequest(request), SnapshotFreshness.Stale, cancellationToken);

        /// <summary>Fetches and installs an exact scoped server projection; an older pull never regresses the durable balance.</summary>
        public async Task<FeatureSnapshot<WalletSnapshot>> RefreshAsync(WalletBalanceRequest request, CancellationToken cancellationToken)
        {
            var exact = EnsureRequest(request);
            var key = new ScopedQueryKey(exact.Owner, FeatureNamespace, ConfirmedKey(exact.Currency));
            var read = await reads.GetOrRefreshAsync(key, async token =>
            {
                var result = await remote.ReadAsync(exact, token).ConfigureAwait(false);
                if (result == null || result.Owner != exact.Owner || !SameCurrency(result.Confirmed.Currency, exact.Currency)) throw new WalletOwnerMismatchException();
                codec.ValidateExtensions(result.Confirmed.CopyExtensions());
                return new FeatureSnapshot<WalletRemoteRead>(exact.Owner, result.Confirmed.Revision, FeatureSnapshotState.Available,
                    SnapshotFreshness.Current, result, result.Confirmed.ConfirmedAtMilliseconds, null);
            }, cancellationToken).ConfigureAwait(false);
            await ApplyConfirmedAsync(exact.Owner, read.Value!.Confirmed, cancellationToken).ConfigureAwait(false);
            return await ReadSnapshotAsync(exact, SnapshotFreshness.Current, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Only a fresh injected backend authorization may admit a spend intent. The local cached balance is never used as authority.</summary>
        public async Task<FeatureSnapshot<WalletSnapshot>> SubmitSpendIntentAsync(WalletSpendRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var exactOwner = EnsureOwner(request.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, request.Intent.Currency, cancellationToken).ConfigureAwait(false);
                if (current.Pending != null && current.Pending.Status != WalletPendingStatus.Rejected)
                {
                    if (Equivalent(current.Pending, request)) return await ReadSnapshotAsync(new WalletBalanceRequest(exactOwner, request.Intent.Currency), SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
                    throw new WalletConflictException("A wallet spend is already pending for this currency and captured scope.");
                }
                if (current.Pending != null && current.Pending.Status == WalletPendingStatus.Rejected && Equivalent(current.Pending, request))
                    return await ReadSnapshotAsync(new WalletBalanceRequest(exactOwner, request.Intent.Currency), SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
                if (current.Confirmed == null || current.Confirmed.Revision != request.Intent.ExpectedRevision)
                    throw new WalletOfflineSpendException();

                WalletSpendAuthorization authorization;
                try { authorization = await remote.AuthorizeSpendAsync(request, cancellationToken).ConfigureAwait(false); }
                catch (WalletOfflineSpendException) { throw; }
                if (authorization == null || authorization.Owner != exactOwner || authorization.OperationId != request.OperationId ||
                    !string.Equals(authorization.BusinessSource, request.BusinessSource, StringComparison.Ordinal) || !Equivalent(authorization.Intent, request.Intent))
                    throw new WalletOwnerMismatchException();

                var body = codec.EncodeSpendCommand(request.Intent);
                ValidatePayload(body, "wallet spend");
                var draft = new CommandDraft(exactOwner.Owner, request.OperationId, request.StreamId, "wallet.spend", 1, 1, body);
                await commands.CommitAsync(request.BusinessSource, draft, (transaction, localRevision) =>
                {
                    var pending = new PendingWalletSpend(request.OperationId, request.StreamId, request.BusinessSource, request.Intent,
                        localRevision, WalletPendingStatus.AwaitingReceipt, null, null);
                    var encoded = codec.EncodePending(pending);
                    ValidatePayload(encoded, "pending wallet spend");
                    state.Upsert(transaction, new DurableFeatureMutation(exactOwner, FeatureNamespace, PendingKey(request.Intent.Currency),
                        localRevision, Now(), encoded, Array.Empty<byte>()));
                }, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(new WalletBalanceRequest(exactOwner, request.Intent.Currency), SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Moves only the matching local intent to receipt-accepted. The balance changes only when a confirming pull arrives.</summary>
        public async Task<FeatureSnapshot<WalletSnapshot>> MarkAcceptedAwaitingPullAsync(WalletSpendAcceptance acceptance, WalletCurrency currency, CancellationToken cancellationToken)
        {
            if (acceptance == null) throw new ArgumentNullException(nameof(acceptance));
            var exactOwner = EnsureOwner(acceptance.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, currency, cancellationToken).ConfigureAwait(false);
                var pending = current.Pending;
                if (pending == null || pending.OperationId != acceptance.OperationId) throw new WalletConflictException("The receipt does not match a durable wallet spend.");
                if (pending.Status == WalletPendingStatus.Rejected || acceptance.ResultingRevision <= pending.Intent.ExpectedRevision) throw new WalletConflictException("The receipt cannot advance this wallet spend.");
                if (pending.Status == WalletPendingStatus.AcceptedAwaitingPull && pending.AcceptedRevision != acceptance.ResultingRevision)
                    throw new WalletConflictException("The retained receipt conflicts with the accepted wallet revision.");
                var updated = new PendingWalletSpend(pending.OperationId, pending.StreamId, pending.BusinessSource, pending.Intent,
                    pending.LocalRevision, WalletPendingStatus.AcceptedAwaitingPull, acceptance.ResultingRevision, null);
                await WritePendingAsync(exactOwner, updated, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(new WalletBalanceRequest(exactOwner, currency), SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        public async Task<FeatureSnapshot<WalletSnapshot>> MarkRejectedAsync(WalletSpendRejection rejection, WalletCurrency currency, CancellationToken cancellationToken)
        {
            if (rejection == null) throw new ArgumentNullException(nameof(rejection));
            var exactOwner = EnsureOwner(rejection.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, currency, cancellationToken).ConfigureAwait(false);
                var pending = current.Pending;
                if (pending == null || pending.OperationId != rejection.OperationId) throw new WalletConflictException("The rejection does not match a durable wallet spend.");
                if (pending.Status == WalletPendingStatus.AcceptedAwaitingPull) throw new WalletConflictException("An accepted wallet receipt cannot be replaced by a rejection.");
                if (pending.Status == WalletPendingStatus.Rejected && !string.Equals(pending.RejectionCode, rejection.Code, StringComparison.Ordinal))
                    throw new WalletConflictException("The retained wallet rejection conflicts with the response.");
                var updated = new PendingWalletSpend(pending.OperationId, pending.StreamId, pending.BusinessSource, pending.Intent,
                    pending.LocalRevision, WalletPendingStatus.Rejected, null, rejection.Code);
                await WritePendingAsync(exactOwner, updated, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(new WalletBalanceRequest(exactOwner, currency), SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Applies monotonic server state only. It neither debits nor credits from local intent or receipt information.</summary>
        public async Task ApplyConfirmedAsync(ScopedOwnerContext requestedOwner, WalletConfirmedBalance confirmed, CancellationToken cancellationToken)
        {
            var exactOwner = EnsureOwner(requestedOwner);
            if (confirmed == null) throw new ArgumentNullException(nameof(confirmed));
            codec.ValidateExtensions(confirmed.CopyExtensions());
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, confirmed.Currency, cancellationToken).ConfigureAwait(false);
                if (current.Confirmed != null && confirmed.Revision < current.Confirmed.Revision) return;
                if (current.Confirmed != null && confirmed.Revision == current.Confirmed.Revision && !Equivalent(current.Confirmed, confirmed))
                    throw new WalletConflictException("The server supplied different wallet contents for the same revision.");
                var clearPending = current.Pending != null && current.Pending.Status == WalletPendingStatus.AcceptedAwaitingPull &&
                    confirmed.Revision >= current.Pending.AcceptedRevision!.Value;
                await transactions.ExecuteAsync(storageScope, transaction =>
                {
                    if (current.Confirmed == null || confirmed.Revision > current.Confirmed.Revision)
                    {
                        var encoded = codec.EncodeConfirmed(confirmed); ValidatePayload(encoded, "confirmed wallet");
                        state.Upsert(transaction, new DurableFeatureMutation(exactOwner, FeatureNamespace, ConfirmedKey(confirmed.Currency),
                            confirmed.Revision, confirmed.ConfirmedAtMilliseconds, encoded, confirmed.CopyExtensions()));
                    }
                    if (clearPending)
                        state.Upsert(transaction, new DurableFeatureMutation(exactOwner, FeatureNamespace, PendingKey(confirmed.Currency),
                            current.Pending!.LocalRevision, Now(), Array.Empty<byte>(), Array.Empty<byte>()));
                    return true;
                }, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
        }

        public void Dispose() { if (disposed) return; disposed = true; reads.Dispose(); mutation.Dispose(); }

        private async Task<FeatureSnapshot<WalletSnapshot>> ReadSnapshotAsync(WalletBalanceRequest exact, SnapshotFreshness freshness, CancellationToken cancellationToken)
        {
            var durable = await ReadDurableAsync(exact.Owner, exact.Currency, cancellationToken).ConfigureAwait(false);
            if (durable.Confirmed == null && durable.Pending == null)
                return new FeatureSnapshot<WalletSnapshot>(exact.Owner, 0, FeatureSnapshotState.Missing, SnapshotFreshness.Missing, null, null, null);
            var snapshot = new WalletSnapshot(durable.Confirmed, durable.Pending);
            var revision = durable.Confirmed == null ? 0 : durable.Confirmed.Revision;
            if (durable.Pending != null && durable.Pending.Status == WalletPendingStatus.Rejected)
                return new FeatureSnapshot<WalletSnapshot>(exact.Owner, revision, FeatureSnapshotState.Error, freshness, snapshot,
                    durable.Confirmed?.ConfirmedAtMilliseconds, durable.Pending.RejectionCode);
            if (durable.Pending != null)
                return new FeatureSnapshot<WalletSnapshot>(exact.Owner, revision, FeatureSnapshotState.Pending, freshness, snapshot,
                    durable.Confirmed?.ConfirmedAtMilliseconds, null);
            return new FeatureSnapshot<WalletSnapshot>(exact.Owner, revision,
                freshness == SnapshotFreshness.Current ? FeatureSnapshotState.Available : FeatureSnapshotState.Stale,
                freshness, snapshot, durable.Confirmed!.ConfirmedAtMilliseconds, null);
        }

        private async Task<(WalletConfirmedBalance? Confirmed, PendingWalletSpend? Pending)> ReadDurableAsync(ScopedOwnerContext exactOwner, WalletCurrency currency, CancellationToken cancellationToken)
        {
            var confirmed = await state.ReadAsync(exactOwner, FeatureNamespace, ConfirmedKey(currency), cancellationToken).ConfigureAwait(false);
            var pending = await state.ReadAsync(exactOwner, FeatureNamespace, PendingKey(currency), cancellationToken).ConfigureAwait(false);
            WalletConfirmedBalance? decodedConfirmed = null; PendingWalletSpend? decodedPending = null;
            if (confirmed != null)
            {
                codec.ValidateExtensions(confirmed.CopyExtensions());
                decodedConfirmed = codec.DecodeConfirmed(confirmed.Revision, confirmed.ConfirmedAtMilliseconds, confirmed.CopyPayload(), confirmed.CopyExtensions());
                if (decodedConfirmed == null || decodedConfirmed.Revision != confirmed.Revision || !SameCurrency(decodedConfirmed.Currency, currency))
                    throw new InvalidOperationException("The wallet state codec returned an invalid confirmed record.");
            }
            if (pending != null && pending.PayloadLength != 0)
            {
                decodedPending = codec.DecodePending(pending.Revision, pending.CopyPayload());
                if (decodedPending == null || decodedPending.LocalRevision != pending.Revision || !SameCurrency(decodedPending.Intent.Currency, currency))
                    throw new InvalidOperationException("The wallet state codec returned an invalid pending record.");
            }
            return (decodedConfirmed, decodedPending);
        }

        private Task WritePendingAsync(ScopedOwnerContext exactOwner, PendingWalletSpend pending, CancellationToken cancellationToken) =>
            transactions.ExecuteAsync(storageScope, transaction =>
            {
                var encoded = codec.EncodePending(pending); ValidatePayload(encoded, "pending wallet spend");
                state.Upsert(transaction, new DurableFeatureMutation(exactOwner, FeatureNamespace, PendingKey(pending.Intent.Currency),
                    pending.LocalRevision, Now(), encoded, Array.Empty<byte>()));
                return true;
            }, cancellationToken);

        private WalletBalanceRequest EnsureRequest(WalletBalanceRequest request)
        { if (request == null) throw new ArgumentNullException(nameof(request)); EnsureOwner(request.Owner); return request; }
        private ScopedOwnerContext EnsureOwner(ScopedOwnerContext requestedOwner)
        { if (disposed) throw new ObjectDisposedException(nameof(WalletService)); if (requestedOwner != owner) throw new WalletOwnerMismatchException(); return requestedOwner; }
        private long Now()
        {
            var value = nowMilliseconds();
            if (value < 0 || value > 253_402_300_799_999L) throw new ArgumentOutOfRangeException("nowMilliseconds");
            return value;
        }
        private static string ConfirmedKey(WalletCurrency currency) => "balance/" + currency.DefinitionId.Value;
        private static string PendingKey(WalletCurrency currency) => "pending/" + currency.DefinitionId.Value;
        private static void ValidatePayload(byte[]? payload, string label)
        { if (payload == null || payload.Length == 0 || payload.Length > 262_144) throw new InvalidOperationException("The " + label + " codec payload is invalid."); }
        private static bool Matches(StorageScope scope, OwnerScope owner) => string.Equals(scope.BackendNamespace, owner.Backend.Value, StringComparison.Ordinal) &&
            string.Equals(scope.AppId.Value, owner.AppId.ToString(), StringComparison.Ordinal) && string.Equals(scope.AccountId.Value, owner.UserId.ToString(), StringComparison.Ordinal);
        private static bool SameCurrency(WalletCurrency left, WalletCurrency right) => left.DefinitionId.Equals(right.DefinitionId) && left.SemanticKey.Equals(right.SemanticKey);
        private static bool Equivalent(WalletSpendIntent left, WalletSpendIntent right) => SameCurrency(left.Currency, right.Currency) && left.ExpectedRevision == right.ExpectedRevision && left.Amount == right.Amount;
        private static bool Equivalent(PendingWalletSpend pending, WalletSpendRequest request) => pending.OperationId == request.OperationId && pending.StreamId == request.StreamId &&
            string.Equals(pending.BusinessSource, request.BusinessSource, StringComparison.Ordinal) && Equivalent(pending.Intent, request.Intent);
        private static bool Equivalent(WalletConfirmedBalance left, WalletConfirmedBalance right) => SameCurrency(left.Currency, right.Currency) && left.Balance == right.Balance &&
            left.Revision == right.Revision && left.ConfirmedAtMilliseconds == right.ConfirmedAtMilliseconds && Equal(left.CopyExtensions(), right.CopyExtensions());
        private static bool Equal(byte[] left, byte[] right) { if (left.Length != right.Length) return false; var difference = 0; for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i]; return difference == 0; }
    }
}
