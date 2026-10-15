using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Shared;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;
namespace GamePlatform.Features.Daily
{
    /// <summary>Account-owned daily status reads and immutable claim admission; never grants locally.</summary>
    public sealed class DailyService : IDisposable
    {
        private const string Namespace = "daily";
        private const string StatusKey = "status";
        private readonly ScopedOwnerContext owner;
        private readonly StorageScope storageScope;
        private readonly IDailyRemote? remote;
        private readonly IDailyStateCodec codec;
        private readonly IDurableFeatureStateStore state;
        private readonly ISerializedStorageExecutor transactions;
        private readonly Func<long> nowMilliseconds;
        private readonly IDailyClaimCommandCodec claimCodec;
        private readonly SourcedCommandAdmitter admitter;
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly object gate = new object();
        private bool disposed;
        public DailyService(ScopedOwnerContext owner, IDailyRemote? remote, IDailyStateCodec codec, IDurableFeatureStateStore state,
            ISerializedStorageExecutor transactions, Func<long> nowMilliseconds, ClientStreamId streamId, IAtomicCommandStore commands,
            IDailyClaimCommandCodec claimCodec, UuidV7Generator ids)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            this.owner = owner;
            storageScope = new StorageScope(owner.Owner.Backend.Value, new PlatformId(owner.Owner.AppId.ToString()), new PlatformId(owner.Owner.UserId.ToString()));
            this.remote = remote;
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            this.nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
            this.claimCodec = claimCodec ?? throw new ArgumentNullException(nameof(claimCodec));
            admitter = new SourcedCommandAdmitter(owner, streamId, Namespace, state, commands, ids);
        }
        public async Task<FeatureSnapshot<DailyStatus>> ReadAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken)
        {
            var cached = await ReadCachedAsync(requestedOwner, cancellationToken).ConfigureAwait(false);
            if (remote == null)
                return cached;
            try
            {
                return await QueryAsync(requestedOwner, cancellationToken).ConfigureAwait(false);
            }
            catch (DailyOperationException error) when (error.Failure.Kind == RemoteFailureKind.Dependency || error.Failure.Kind == RemoteFailureKind.Unavailable ||
                error.Failure.Kind == RemoteFailureKind.NotSent || error.Failure.Kind == RemoteFailureKind.RateLimited)
            {
                if (cached.Value != null)
                    return await ReadCachedAsync(requestedOwner, cancellationToken).ConfigureAwait(false);
                return new FeatureSnapshot<DailyStatus>(owner, 0, FeatureSnapshotState.Unavailable, SnapshotFreshness.Missing, null, null, error.ResultCode);
            }
        }
        public async Task<FeatureSnapshot<DailyStatus>> ReadCachedAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner);
            using var linked = Link(cancellationToken);
            var cached = await state.ReadAsync(owner, Namespace, StatusKey, linked.Token).ConfigureAwait(false);
            EnsureOwner(requestedOwner);
            linked.Token.ThrowIfCancellationRequested();
            return cached == null ? Missing() : Snapshot(codec.DecodeStatus(cached.CopyPayload()), SnapshotFreshness.Stale, cached.ConfirmedAtMilliseconds);
        }
        public async Task<FeatureSnapshot<DailyStatus>> QueryAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner);
            using var linked = Link(cancellationToken);
            await serial.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                EnsureOwner(requestedOwner);
                if (remote == null)
                    throw Error(RemoteFailureKind.Unavailable, "daily_offline");
                RemoteResult<DailyStatus> result;
                try
                {
                    result = await remote.StatusAsync(linked.Token).ConfigureAwait(false);
                }
                catch (DailyOperationException error)
                {
                    if (error.Failure.Kind == RemoteFailureKind.Authorization)
                        await DeleteStatusAsync(linked.Token).ConfigureAwait(false);
                    throw;
                }
                EnsureOwner(requestedOwner);
                linked.Token.ThrowIfCancellationRequested();
                if (!result.IsSuccess)
                {
                    if (result.Failure.Kind == RemoteFailureKind.Authorization)
                        await DeleteStatusAsync(linked.Token).ConfigureAwait(false);
                    throw new DailyOperationException(result.Failure, "daily_" + result.Failure.Kind.ToString().ToLowerInvariant());
                }
                var value = result.Value!;
                await WriteStatusAsync(requestedOwner, value, linked.Token).ConfigureAwait(false);
                EnsureOwner(requestedOwner);
                linked.Token.ThrowIfCancellationRequested();
                return Snapshot(value, SnapshotFreshness.Current, null);
            }
            finally { serial.Release(); }
        }
        /// <summary>Durably queues one daily.claim for the server-reported date; repeating it returns the original admission.</summary>
        public Task<CommandAdmission> ClaimAsync(ScopedOwnerContext requestedOwner, DailyStatus status, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner);
            if (status == null)
                throw new ArgumentNullException(nameof(status));
            if (!status.Eligible)
                throw Error(RemoteFailureKind.Validation, "daily_not_eligible");
            return AdmitAsync(status, cancellationToken);
        }
        /// <summary>Applies a server-confirmed daily_claimed receipt to the cached status and returns the updated cached snapshot.</summary>
        public async Task<FeatureSnapshot<DailyStatus>> RecordConfirmedAsync(ScopedOwnerContext requestedOwner, DailyClaimEntry entry, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner);
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            using var linked = Link(cancellationToken);
            await serial.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                var cached = await state.ReadAsync(owner, Namespace, StatusKey, linked.Token).ConfigureAwait(false);
                EnsureOwner(requestedOwner);
                linked.Token.ThrowIfCancellationRequested();
                if (cached == null)
                    return Missing();
                var merged = Apply(codec.DecodeStatus(cached.CopyPayload()), entry);
                await WriteStatusAsync(requestedOwner, merged, linked.Token).ConfigureAwait(false);
            }
            finally { serial.Release(); }
            return await ReadCachedAsync(requestedOwner, cancellationToken).ConfigureAwait(false);
        }
        internal static DailyStatus Apply(DailyStatus status, DailyClaimEntry entry)
        {
            var history = new List<DailyClaimEntry> { entry };
            history.AddRange(status.History.Where(h => h.UtcDate != entry.UtcDate).Take(6));
            bool today = entry.UtcDate == status.UtcDate;
            return new DailyStatus(status.UtcDate, status.NextUtcDayAtMilliseconds, !today && status.Eligible, today ? entry.CycleDay : status.CycleDay,
                status.PolicyVersion, status.Reward, history);
        }
        private async Task<CommandAdmission> AdmitAsync(DailyStatus status, CancellationToken cancellationToken)
        {
            using var linked = Link(cancellationToken);
            var body = claimCodec.EncodeClaimCommand(status.UtcDate);
            return await admitter.AdmitAsync("daily.claim:" + status.PolicyVersion + ":" + status.UtcDate, "daily.claim", body, linked.Token).ConfigureAwait(false);
        }
        private Task<bool> WriteStatusAsync(ScopedOwnerContext requestedOwner, DailyStatus value, CancellationToken token)
        {
            var bytes = codec.EncodeStatus(value);
            return transactions.ExecuteAsync(storageScope, tx =>
            {
                lock (gate)
                {
                    EnsureOwner(requestedOwner);
                    token.ThrowIfCancellationRequested();
                    var prior = state.Read(tx, owner, Namespace, StatusKey);
                    state.Upsert(tx, new DurableFeatureMutation(owner, Namespace, StatusKey, checked((prior?.Revision ?? 0) + 1), Now(), bytes, Array.Empty<byte>()));
                    return true;
                }
            }, token);
        }
        private Task<bool> DeleteStatusAsync(CancellationToken token) => transactions.ExecuteAsync(storageScope, tx =>
        {
            lock (gate)
            {
                EnsureOwner(owner);
                token.ThrowIfCancellationRequested();
                state.Delete(tx, owner, Namespace, StatusKey);
                return true;
            }
        }, token);
        private static DailyOperationException Error(RemoteFailureKind kind, string code) => new DailyOperationException(new RemoteFailure(kind), code);
        private long Now()
        {
            long value = nowMilliseconds();
            if (value < 0 || value > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));
            return value;
        }
        private FeatureSnapshot<DailyStatus> Missing() => new FeatureSnapshot<DailyStatus>(owner, 0, SnapshotFreshness.Missing, null);
        private FeatureSnapshot<DailyStatus> Snapshot(DailyStatus value, SnapshotFreshness freshness, long? confirmedAt) =>
            new FeatureSnapshot<DailyStatus>(owner, 0, freshness == SnapshotFreshness.Current ? FeatureSnapshotState.Available : FeatureSnapshotState.Stale, freshness, value, confirmedAt, null);
        private CancellationTokenSource Link(CancellationToken token)
        {
            lock (gate)
            {
                EnsureOwner(owner);
                return CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            }
        }
        private void EnsureOwner(ScopedOwnerContext value)
        {
            lock (gate)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(DailyService));
                if (value != owner)
                    throw Error(RemoteFailureKind.Authorization, "daily_owner_mismatch");
            }
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                    return;
                disposed = true;
            }
            lifetime.Cancel();
            admitter.Dispose();
        }
    }
}
