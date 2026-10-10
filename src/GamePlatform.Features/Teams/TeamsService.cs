#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;

namespace GamePlatform.Features.Teams
{
    public interface ITeamsRemote
    {
        Task<RemoteResult<TeamsResponse>> QueryAsync(TeamsQuery query, CancellationToken cancellationToken);
        Task<RemoteResult<TeamsResponse>> ExecuteAsync(TeamsCommand command, CancellationToken cancellationToken);
    }

    public interface ITeamsStateCodec
    {
        byte[] EncodeResponse(TeamsResponse response);
        TeamsResponse DecodeResponse(byte[] bytes);
        byte[] EncodeCommand(TeamsCommand command);
        TeamsCommand DecodeCommand(byte[] bytes);
    }

    public sealed class TeamsOperationException : InvalidOperationException
    {
        public TeamsOperationException(RemoteFailure failure, string resultCode)
            : base("The Teams operation could not be confirmed: " + SafeCode(resultCode))
        { Failure = failure; ResultCode = SafeCode(resultCode); }
        public RemoteFailure Failure { get; }
        public string ResultCode { get; }
        private static string SafeCode(string value) => !string.IsNullOrEmpty(value) && value.Length <= 96 &&
            value.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '.') ? value : "teams_unavailable";
    }

    /// <summary>Account-owned online social operations with a bounded durable stale cache and explicit retry journal.</summary>
    public sealed class TeamsService : IDisposable
    {
        private const string Namespace = "teams";
        private const string Pending = "pending-command";
        private const string Epoch = "cache-epoch";
        private readonly ScopedOwnerContext owner;
        private readonly StorageScope storageScope;
        private readonly ITeamsRemote? remote;
        private readonly ITeamsStateCodec codec;
        private readonly IDurableFeatureStateStore state;
        private readonly ISerializedStorageExecutor transactions;
        private readonly Func<long> nowMilliseconds;
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly object gate = new object();
        private bool disposed;

        public TeamsService(ScopedOwnerContext owner, ITeamsRemote? remote, ITeamsStateCodec codec,
            IDurableFeatureStateStore state, ISerializedStorageExecutor transactions, Func<long> nowMilliseconds)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            this.owner = owner;
            storageScope = new StorageScope(owner.Owner.Backend.Value, new PlatformId(owner.Owner.AppId.ToString()), new PlatformId(owner.Owner.UserId.ToString()));
            this.remote = remote;
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            this.nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
        }

        public async Task<FeatureSnapshot<TeamsResponse>> ReadCachedAsync(ScopedOwnerContext requestedOwner, TeamsQuery query, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner);
            if (query == null) throw new ArgumentNullException(nameof(query));
            string? key = CacheKey(query);
            if (key == null) return Missing();
            var cached = await state.ReadAsync(owner, Namespace, key, cancellationToken).ConfigureAwait(false);
            EnsureOwner(requestedOwner);
            if (cached == null) return Missing();
            var value = codec.DecodeResponse(cached.CopyPayload());
            Validate(value, null);
            // Cache slots are bounded by view. A different team never inherits the previous team's data.
            if (query.TeamId.HasValue && value.Team?.TeamId != query.TeamId.Value) return Missing();
            return Snapshot(value, SnapshotFreshness.Stale);
        }

        /// <summary>Returns an interrupted mutation for explicit user-driven retry. Never sends it automatically.</summary>
        public async Task<TeamsCommand?> ReadPendingAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner);
            var pending = await state.ReadAsync(owner, Namespace, Pending, cancellationToken).ConfigureAwait(false);
            EnsureOwner(requestedOwner);
            return pending == null ? null : codec.DecodeCommand(pending.CopyPayload());
        }

        public async Task<FeatureSnapshot<TeamsResponse>> QueryAsync(ScopedOwnerContext requestedOwner, TeamsQuery query, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner);
            if (query == null) throw new ArgumentNullException(nameof(query));
            using var linked = Link(cancellationToken);
            await serial.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                EnsureOwner(requestedOwner);
                if (remote == null) throw new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Unavailable), "teams_offline");
                var epoch = await state.ReadAsync(owner, Namespace, Epoch, linked.Token).ConfigureAwait(false);
                RemoteResult<TeamsResponse> result;
                try { result = await remote.QueryAsync(query, linked.Token).ConfigureAwait(false); }
                catch (TeamsOperationException error)
                {
                    if (error.Failure.Kind == RemoteFailureKind.Authorization) await InvalidateAsync(linked.Token).ConfigureAwait(false);
                    throw;
                }
                EnsureOwner(requestedOwner);
                if (!result.IsSuccess)
                {
                    if (result.Failure.Kind == RemoteFailureKind.Authorization) await InvalidateAsync(linked.Token).ConfigureAwait(false);
                    throw new TeamsOperationException(result.Failure, "teams_" + result.Failure.Kind.ToString().ToLowerInvariant());
                }
                var value = result.Value!;
                Validate(value, null);
                if (query.TeamId.HasValue && value.Team?.TeamId != query.TeamId.Value)
                    throw new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Protocol), "teams_query_mismatch");
                var key = CacheKey(query);
                await SaveCacheAsync(key, value, epoch?.Revision ?? 0, linked.Token).ConfigureAwait(false);
                EnsureOwner(requestedOwner);
                return Snapshot(value, SnapshotFreshness.Current);
            }
            finally { serial.Release(); }
        }

        public async Task<FeatureSnapshot<TeamsResponse>> ExecuteAsync(ScopedOwnerContext requestedOwner, TeamsCommand command, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner);
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (remote == null) throw new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Unavailable), "teams_offline");
            var bytes = codec.EncodeCommand(command); // Closed validation before durable admission or network IO.
            using var linked = Link(cancellationToken);
            await serial.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                EnsureOwner(requestedOwner);
                await transactions.ExecuteAsync(storageScope, transaction =>
                {
                    lock (gate)
                    {
                        EnsureOwner(requestedOwner);
                        var existing = state.Read(transaction, owner, Namespace, Pending);
                        if (existing != null && !existing.CopyPayload().SequenceEqual(bytes))
                            throw new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Conflict), "teams_pending_command");
                        if (existing == null) state.Upsert(transaction, new DurableFeatureMutation(owner, Namespace, Pending, 0, Now(), bytes, Array.Empty<byte>()));
                        return true;
                    }
                }, linked.Token).ConfigureAwait(false);
                RemoteResult<TeamsResponse> result;
                try { result = await remote.ExecuteAsync(command, linked.Token).ConfigureAwait(false); }
                catch (TeamsOperationException error)
                {
                    // Disabled app/account ownership is checked before retained receipts. A prior
                    // uncertain attempt may have committed, so keep its identity until access returns.
                    if (error.Failure.Kind == RemoteFailureKind.Authorization && error.ResultCode == "teams.owner_unavailable")
                        await InvalidateAsync(linked.Token).ConfigureAwait(false);
                    else if (IsTerminal(error.Failure.Kind)) await ClearPendingAsync(error.Failure.Kind, linked.Token).ConfigureAwait(false);
                    throw;
                }
                EnsureOwner(requestedOwner);
                if (!result.IsSuccess)
                {
                    if (IsTerminal(result.Failure.Kind)) await ClearPendingAsync(result.Failure.Kind, linked.Token).ConfigureAwait(false);
                    throw new TeamsOperationException(result.Failure, "teams_" + result.Failure.Kind.ToString().ToLowerInvariant());
                }
                var value = result.Value!;
                Validate(value, command.OperationId.Value);
                // Receipts are operation acknowledgements; the next query establishes the current social view.
                await transactions.ExecuteAsync(storageScope, transaction =>
                {
                    lock (gate)
                    {
                        EnsureOwner(requestedOwner);
                        state.Delete(transaction, owner, Namespace, Pending);
                        InvalidateCache(transaction, state, owner);
                        return true;
                    }
                }, linked.Token).ConfigureAwait(false);
                EnsureOwner(requestedOwner);
                return Snapshot(value, SnapshotFreshness.Current);
            }
            finally { serial.Release(); }
        }

        private Task<bool> ClearPendingAsync(RemoteFailureKind failure, CancellationToken token) => transactions.ExecuteAsync(storageScope, transaction =>
        {
            lock (gate)
            {
                EnsureOwner(owner);
                state.Delete(transaction, owner, Namespace, Pending);
                if (failure == RemoteFailureKind.Authorization) InvalidateCache(transaction, state, owner);
                return true;
            }
        }, token);

        private Task<bool> InvalidateAsync(CancellationToken token) => transactions.ExecuteAsync(storageScope, transaction =>
        { lock (gate) { EnsureOwner(owner); InvalidateCache(transaction, state, owner); return true; } }, token);

        private Task<bool> SaveCacheAsync(string? key, TeamsResponse value, long expectedEpoch, CancellationToken token)
        {
            byte[] bytes = codec.EncodeResponse(value);
            return transactions.ExecuteAsync(storageScope, transaction =>
            {
                lock (gate)
                {
                    EnsureOwner(owner);
                    if ((state.Read(transaction, owner, Namespace, Epoch)?.Revision ?? 0) != expectedEpoch)
                        throw new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Conflict), "teams_refresh_required");
                    if (key == null) return true;
                    if (key == "mine")
                    {
                        var prior = state.Read(transaction, owner, Namespace, key);
                        var previous = prior == null ? null : codec.DecodeResponse(prior.CopyPayload());
                        if (value.Membership == null || previous?.Membership?.TeamId != value.Membership.TeamId)
                            InvalidateCache(transaction, state, owner);
                    }
                    var current = state.Read(transaction, owner, Namespace, key);
                    // Slot revisions describe local writes; team revisions can legitimately change when switching teams.
                    long revision = current == null ? 0 : checked(current.Revision + 1);
                    state.Upsert(transaction, new DurableFeatureMutation(owner, Namespace, key, revision, Now(), bytes, Array.Empty<byte>()));
                    return true;
                }
            }, token);
        }

        private void Validate(TeamsResponse value, Guid? operation)
        {
            if (value == null || value.AppId != owner.Owner.AppId.Value || value.AccountId != owner.Owner.UserId.Value || value.OperationId != operation ||
                value.Membership != null && value.Membership.PlayerId != value.PlayerId || value.ServerTimeMilliseconds > 253_402_300_799_999L)
                throw new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Protocol), "teams_owner_mismatch");
        }
        private static readonly string[] Views = { "discover", "mine", "team", "members", "messages", "requests", "invites", "help" };
        public static void InvalidateCache(ILocalStorageTransaction transaction, IDurableFeatureStateStore state, ScopedOwnerContext owner)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (string key in Views) state.Delete(transaction, owner, Namespace, key);
            var previous = state.Read(transaction, owner, Namespace, Epoch);
            state.Upsert(transaction, new DurableFeatureMutation(owner, Namespace, Epoch, checked((previous?.Revision ?? 0) + 1), 0, Array.Empty<byte>(), Array.Empty<byte>()));
        }
        private static string? CacheKey(TeamsQuery query) => query.Cursor == null && string.IsNullOrEmpty(query.Search) && query.PageSize == 30 ? query.View : null;
        private static bool IsTerminal(RemoteFailureKind kind) => kind == RemoteFailureKind.Validation || kind == RemoteFailureKind.Authorization ||
            kind == RemoteFailureKind.Conflict || kind == RemoteFailureKind.RateLimited;
        private long Now() { long value = nowMilliseconds(); PlatformNumbers.UnixMilliseconds(value); return value; }
        private FeatureSnapshot<TeamsResponse> Missing() => new FeatureSnapshot<TeamsResponse>(owner, 0, SnapshotFreshness.Missing, null);
        private FeatureSnapshot<TeamsResponse> Snapshot(TeamsResponse value, SnapshotFreshness freshness) => new FeatureSnapshot<TeamsResponse>(owner, value.Revision,
            freshness == SnapshotFreshness.Current ? FeatureSnapshotState.Available : FeatureSnapshotState.Stale, freshness, value, value.ServerTimeMilliseconds, null);
        private CancellationTokenSource Link(CancellationToken token) { lock (gate) { EnsureOwner(owner); return CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); } }
        private void EnsureOwner(ScopedOwnerContext value)
        {
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(TeamsService));
                if (value != owner) throw new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Authorization), "teams_owner_mismatch");
            }
        }
        public void Dispose() { lock (gate) { if (disposed) return; disposed = true; } lifetime.Cancel(); }
    }
}
