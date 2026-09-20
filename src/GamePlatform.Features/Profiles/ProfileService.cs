#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Shared;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Features.Profiles
{
    /// <summary>Portable, account-scoped profile projection and edit coordinator. It owns no HTTP or SQLite implementation.</summary>
    public sealed class ProfileService : IDisposable
    {
        private const string FeatureNamespace = "profiles";
        private const string ConfirmedKey = "self";
        private const string PendingKey = "self.pending";
        private readonly ScopedOwnerContext owner;
        private readonly StorageScope storageScope;
        private readonly IDurableFeatureStateStore state;
        private readonly ISerializedStorageExecutor transactions;
        private readonly IAtomicCommandStore commands;
        private readonly IProfileStateCodec codec;
        private readonly IProfileRemote remote;
        private readonly DisposableQueryCache<ProfileRemoteRead> reads;
        private readonly Func<long> nowMilliseconds;
        private readonly SemaphoreSlim mutation = new SemaphoreSlim(1, 1);
        private bool disposed;

        public ProfileService(ScopedOwnerContext owner, StorageScope storageScope, IDurableFeatureStateStore state,
            ISerializedStorageExecutor transactions, IAtomicCommandStore commands, IProfileStateCodec codec,
            IProfileRemote remote, int refreshCacheCapacity, TimeSpan refreshCacheLifetime, Func<long> nowMilliseconds)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!Matches(storageScope, owner.Owner)) throw new ArgumentException("The profile storage scope does not match the captured owner.", nameof(storageScope));
            this.owner = owner; this.storageScope = storageScope;
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.remote = remote ?? throw new ArgumentNullException(nameof(remote));
            this.nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
            reads = new DisposableQueryCache<ProfileRemoteRead>(refreshCacheCapacity, refreshCacheLifetime, this.nowMilliseconds);
        }

        /// <summary>Reads durable state only. A cached confirmed profile is explicitly stale until a successful refresh.</summary>
        public Task<FeatureSnapshot<ProfileSnapshot>> ReadCachedAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken) =>
            ReadSnapshotAsync(EnsureOwner(requestedOwner), SnapshotFreshness.Stale, cancellationToken);

        /// <summary>Coalesces an exact owner/view/generation read and never lets a foreign late result publish.</summary>
        public async Task<FeatureSnapshot<ProfileSnapshot>> RefreshAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken)
        {
            var exactOwner = EnsureOwner(requestedOwner);
            var key = new ScopedQueryKey(exactOwner, FeatureNamespace, ConfirmedKey);
            var read = await reads.GetOrRefreshAsync(key, async token =>
            {
                var result = await remote.ReadAsync(exactOwner, token).ConfigureAwait(false);
                if (result == null || result.Owner != exactOwner) throw new ProfileOwnerMismatchException();
                codec.ValidateExtensions(result.Profile.CopyExtensions());
                return new FeatureSnapshot<ProfileRemoteRead>(exactOwner, result.Profile.Revision,
                    FeatureSnapshotState.Available, SnapshotFreshness.Current, result, result.Profile.UpdatedAtMilliseconds, null);
            }, cancellationToken).ConfigureAwait(false);
            await ApplyConfirmedAsync(exactOwner, read.Value!.Profile, cancellationToken).ConfigureAwait(false);
            return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Current, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Stores one exact-revision local intent and its immutable outbox command in the same caller-provided transaction seam.</summary>
        public async Task<FeatureSnapshot<ProfileSnapshot>> EditAsync(ProfileUpdateRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var exactOwner = EnsureOwner(request.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                if (current.Pending != null)
                {
                    if (current.Pending.OperationId == request.OperationId && current.Pending.StreamId == request.StreamId &&
                        string.Equals(current.Pending.BusinessSource, request.BusinessSource, StringComparison.Ordinal) &&
                        Equivalent(current.Pending.Patch, request.Patch))
                        return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
                    throw new ProfileConflictException("A profile edit is already pending for this account/view generation.");
                }
                var expected = current.Confirmed == null ? 0 : current.Confirmed.Revision;
                if (request.Patch.ExpectedRevision != expected) throw new ProfileConflictException("The profile edit expected revision is not the durable confirmed revision.");
                var body = codec.EncodePatchCommand(request.Patch);
                if (body == null || body.Length == 0 || body.Length > 262_144) throw new InvalidOperationException("The profile command codec returned an invalid semantic body.");
                var draft = new CommandDraft(exactOwner.Owner, request.OperationId, request.StreamId, "profile.patch", 1, 1, body);
                await commands.CommitAsync(request.BusinessSource, draft, (transaction, localRevision) =>
                {
                    var pending = new PendingProfileEdit(request.OperationId, request.StreamId, request.BusinessSource, request.Patch,
                        localRevision, ProfilePendingStatus.AwaitingReceipt, null);
                    var encoded = codec.EncodePending(pending);
                    ValidatePayload(encoded, "pending profile");
                    state.Upsert(transaction, new DurableFeatureMutation(exactOwner, FeatureNamespace, PendingKey,
                        localRevision, Now(), encoded, Array.Empty<byte>()));
                }, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Applies a retained command receipt without treating it as a profile projection. Pull must still confirm the fields.</summary>
        public async Task<FeatureSnapshot<ProfileSnapshot>> MarkAcceptedAwaitingPullAsync(ProfileUpdateAcceptance acceptance, CancellationToken cancellationToken)
        {
            if (acceptance == null) throw new ArgumentNullException(nameof(acceptance));
            var exactOwner = EnsureOwner(acceptance.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                var pending = current.Pending;
                if (pending == null || pending.OperationId != acceptance.OperationId)
                    throw new ProfileConflictException("The accepted receipt does not match the durable pending profile edit.");
                if (acceptance.ResultingRevision <= pending.Patch.ExpectedRevision)
                    throw new ProfileConflictException("The accepted receipt did not advance the profile revision.");
                if (pending.Status == ProfilePendingStatus.Conflict)
                    throw new ProfileConflictException("A conflicted profile edit cannot be promoted by a late receipt.");
                if (pending.Status == ProfilePendingStatus.AcceptedAwaitingPull && pending.AcceptedRevision != acceptance.ResultingRevision)
                    throw new ProfileConflictException("The retained profile receipt conflicts with the accepted revision.");
                var updated = new PendingProfileEdit(pending.OperationId, pending.StreamId, pending.BusinessSource, pending.Patch,
                    pending.LocalRevision, ProfilePendingStatus.AcceptedAwaitingPull, acceptance.ResultingRevision);
                await WritePendingAsync(exactOwner, updated, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Installs only monotonic server-confirmed state through the same borrowed projection mutation used by private-feed callers.</summary>
        public async Task ApplyConfirmedAsync(ScopedOwnerContext requestedOwner, ProfileConfirmed confirmed, CancellationToken cancellationToken)
        {
            var exactOwner = EnsureOwner(requestedOwner);
            if (confirmed == null) throw new ArgumentNullException(nameof(confirmed));
            codec.ValidateExtensions(confirmed.CopyExtensions());
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                if (current.Confirmed != null && confirmed.Revision < current.Confirmed.Revision) return;
                if (current.Confirmed != null && confirmed.Revision == current.Confirmed.Revision)
                {
                    if (!Equivalent(current.Confirmed, confirmed))
                        throw new ProfileConflictException("The server supplied different profile contents for the same revision.");
                    return;
                }

                await transactions.ExecuteAsync(storageScope, transaction =>
                {
                    ApplyConfirmedProjection(transaction, exactOwner, current.Confirmed == null ? (long?)null : current.Confirmed.Revision, confirmed);
                    return true;
                }, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
        }

        /// <summary>
        /// Applies a validated server profile in the caller-owned private-feed transaction. The caller supplies the
        /// confirmed durable revision read in that same transaction; null means absent while zero is a real
        /// confirmed revision. This method neither advances nor acknowledges a cursor.
        /// </summary>
        public void ApplyConfirmedProjection(ILocalStorageTransaction transaction, ScopedOwnerContext requestedOwner,
            long? durablePriorRevision, ProfileConfirmed confirmed)
        {
            var exactOwner = EnsureOwner(requestedOwner);
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (!transaction.Scope.Equals(storageScope)) throw new StorageException(StorageFailure.InvalidOwner, "The profile transaction belongs to another scope.");
            if (durablePriorRevision.HasValue && durablePriorRevision.Value < 0) throw new ArgumentOutOfRangeException(nameof(durablePriorRevision));
            if (confirmed == null) throw new ArgumentNullException(nameof(confirmed));
            if (durablePriorRevision.HasValue && confirmed.Revision <= durablePriorRevision.Value)
                throw new ProfileConflictException("A borrowed confirmed profile projection must advance its durable revision.");
            var extensions = confirmed.CopyExtensions();
            codec.ValidateExtensions(extensions);
            var payload = codec.EncodeConfirmed(confirmed);
            ValidatePayload(payload, "confirmed profile");
            state.Upsert(transaction, new DurableFeatureMutation(exactOwner, FeatureNamespace, ConfirmedKey,
                ToStorageRevision(confirmed.Revision), confirmed.UpdatedAtMilliseconds, payload, extensions));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; reads.Dispose(); mutation.Dispose();
        }

        private async Task<FeatureSnapshot<ProfileSnapshot>> ReadSnapshotAsync(ScopedOwnerContext exactOwner, SnapshotFreshness freshness, CancellationToken cancellationToken)
        {
            var durable = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
            if (durable.Confirmed == null && durable.Pending == null)
                return new FeatureSnapshot<ProfileSnapshot>(exactOwner, 0, FeatureSnapshotState.Missing, SnapshotFreshness.Missing, null, null, null);
            var value = new ProfileSnapshot(durable.Confirmed, durable.Pending);
            var revision = durable.Confirmed == null ? 0 : durable.Confirmed.Revision;
            if (durable.Pending != null && durable.Pending.Status == ProfilePendingStatus.Conflict)
                return new FeatureSnapshot<ProfileSnapshot>(exactOwner, revision, FeatureSnapshotState.Error, freshness, value,
                    durable.Confirmed?.UpdatedAtMilliseconds, "profile_revision_conflict");
            if (durable.Pending != null)
                return new FeatureSnapshot<ProfileSnapshot>(exactOwner, revision, FeatureSnapshotState.Pending, freshness, value,
                    durable.Confirmed?.UpdatedAtMilliseconds, null);
            return new FeatureSnapshot<ProfileSnapshot>(exactOwner, revision,
                freshness == SnapshotFreshness.Current ? FeatureSnapshotState.Available : FeatureSnapshotState.Stale,
                freshness, value, durable.Confirmed!.UpdatedAtMilliseconds, null);
        }

        private async Task<(ProfileConfirmed? Confirmed, PendingProfileEdit? Pending)> ReadDurableAsync(ScopedOwnerContext exactOwner, CancellationToken cancellationToken)
        {
            var confirmed = await state.ReadAsync(exactOwner, FeatureNamespace, ConfirmedKey, cancellationToken).ConfigureAwait(false);
            var pending = await state.ReadAsync(exactOwner, FeatureNamespace, PendingKey, cancellationToken).ConfigureAwait(false);
            ProfileConfirmed? decodedConfirmed = null; PendingProfileEdit? decodedPending = null;
            if (confirmed != null)
            {
                codec.ValidateExtensions(confirmed.CopyExtensions());
                var semanticRevision = FromStorageRevision(confirmed.Revision);
                decodedConfirmed = codec.DecodeConfirmed(semanticRevision, confirmed.ConfirmedAtMilliseconds, confirmed.CopyPayload(), confirmed.CopyExtensions());
                if (decodedConfirmed == null || decodedConfirmed.Revision != semanticRevision) throw new InvalidOperationException("The profile state codec returned an invalid confirmed record.");
            }
            if (pending != null && pending.PayloadLength != 0)
            {
                decodedPending = codec.DecodePending(pending.Revision, pending.CopyPayload());
                if (decodedPending == null || decodedPending.LocalRevision != pending.Revision) throw new InvalidOperationException("The profile state codec returned an invalid pending record.");
                // A private-feed caller may commit the confirmed profile and cursor atomically without owning this
                // pending row. On every reopen/read, confirmed state suppresses only the matching accepted receipt;
                // an unreceipted local edit displaced by a newer server revision remains an explicit conflict.
                if (decodedConfirmed != null && decodedPending.Status == ProfilePendingStatus.AcceptedAwaitingPull &&
                    decodedConfirmed.Revision >= decodedPending.AcceptedRevision!.Value)
                    decodedPending = null;
                else if (decodedConfirmed != null && decodedPending.Status != ProfilePendingStatus.AcceptedAwaitingPull &&
                    decodedConfirmed.Revision > decodedPending.Patch.ExpectedRevision && decodedPending.Status != ProfilePendingStatus.Conflict)
                    decodedPending = new PendingProfileEdit(decodedPending.OperationId, decodedPending.StreamId, decodedPending.BusinessSource,
                        decodedPending.Patch, decodedPending.LocalRevision, ProfilePendingStatus.Conflict, null);
            }
            return (decodedConfirmed, decodedPending);
        }

        private Task WritePendingAsync(ScopedOwnerContext exactOwner, PendingProfileEdit pending, CancellationToken cancellationToken) =>
            transactions.ExecuteAsync(storageScope, transaction =>
            {
                var encoded = codec.EncodePending(pending);
                ValidatePayload(encoded, "pending profile");
                state.Upsert(transaction, new DurableFeatureMutation(exactOwner, FeatureNamespace, PendingKey,
                    pending.LocalRevision, Now(), encoded, Array.Empty<byte>()));
                return true;
            }, cancellationToken);

        private ScopedOwnerContext EnsureOwner(ScopedOwnerContext requestedOwner)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ProfileService));
            if (requestedOwner != owner) throw new ProfileOwnerMismatchException();
            return requestedOwner;
        }
        private long Now() { var value = nowMilliseconds(); ProfileValidation.Timestamp(value, "nowMilliseconds"); return value; }
        private static void ValidatePayload(byte[]? payload, string label)
        {
            if (payload == null || payload.Length == 0 || payload.Length > 262_144) throw new InvalidOperationException("The " + label + " codec payload is invalid.");
        }
        private static bool Matches(StorageScope scope, OwnerScope owner) => string.Equals(scope.BackendNamespace, owner.Backend.Value, StringComparison.Ordinal) &&
            string.Equals(scope.AppId.Value, owner.AppId.ToString(), StringComparison.Ordinal) && string.Equals(scope.AccountId.Value, owner.UserId.ToString(), StringComparison.Ordinal);
        private static bool Equivalent(ProfileConfirmed left, ProfileConfirmed right) => left.Revision == right.Revision && left.UpdatedAtMilliseconds == right.UpdatedAtMilliseconds &&
            left.HasAvatarKey == right.HasAvatarKey && left.HasLocale == right.HasLocale && string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
            string.Equals(left.AvatarKey, right.AvatarKey, StringComparison.Ordinal) && string.Equals(left.Locale, right.Locale, StringComparison.Ordinal) && Equal(left.CopyExtensions(), right.CopyExtensions());
        private static bool Equivalent(ProfilePatch left, ProfilePatch right) => left.ExpectedRevision == right.ExpectedRevision &&
            left.HasDisplayName == right.HasDisplayName && left.HasAvatarKey == right.HasAvatarKey && left.HasLocale == right.HasLocale &&
            string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) && string.Equals(left.AvatarKey, right.AvatarKey, StringComparison.Ordinal) &&
            string.Equals(left.Locale, right.Locale, StringComparison.Ordinal);
        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            var difference = 0; for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index]; return difference == 0;
        }
        // gp_feature_state reserves positive row revisions for durable-record identity. This is an envelope only:
        // semantic profile revision zero round-trips as zero and is never surfaced as revision one.
        private static long ToStorageRevision(long semanticRevision) => semanticRevision == long.MaxValue ? throw new ProfileConflictException("The profile revision is exhausted.") : checked(semanticRevision + 1);
        private static long FromStorageRevision(long storageRevision) => storageRevision <= 0 ? throw new ProfileConflictException("The profile storage revision is invalid.") : storageRevision - 1;
    }
}
