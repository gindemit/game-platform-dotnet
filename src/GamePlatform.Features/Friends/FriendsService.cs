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
namespace GamePlatform.Features.Friends
{
    public interface IFriendsRemote
    {
        Task<RemoteResult<FriendsResponse>> QueryAsync(FriendsQuery query, CancellationToken token);
        Task<RemoteResult<FriendsResponse>> ExecuteAsync(FriendsCommand command, CancellationToken token);
    }
    public interface IFriendsStateCodec
    {
        byte[] EncodeResponse(FriendsResponse value);
        FriendsResponse DecodeResponse(byte[] bytes);
        byte[] EncodeCommand(FriendsCommand value);
        FriendsCommand DecodeCommand(byte[] bytes);
    }
    public sealed class FriendsOperationException : InvalidOperationException
    {
        public FriendsOperationException(RemoteFailure failure, string resultCode, Guid? retainedOperationId = null, long? retryAfterMilliseconds = null)
            : base("The Friends operation could not be confirmed: " + SafeCode(resultCode))
        {
            if (retainedOperationId == Guid.Empty || retryAfterMilliseconds < 0 || retryAfterMilliseconds > 253402300799999L) throw new ArgumentException("Invalid rejection metadata.");
            Failure = failure; ResultCode = SafeCode(resultCode); RetainedOperationId = IsBusinessRejection(failure.Kind, ResultCode) ? retainedOperationId : null; RetryAfterMilliseconds = retryAfterMilliseconds;
        }
        public RemoteFailure Failure { get; }
        public string ResultCode { get; }
        public Guid? RetainedOperationId { get; }
        public long? RetryAfterMilliseconds { get; }
        private static bool IsBusinessRejection(RemoteFailureKind kind, string code) => (kind == RemoteFailureKind.Validation || kind == RemoteFailureKind.Conflict || kind == RemoteFailureKind.RateLimited) && (code == "friends.target_unavailable" || code == "friends.already_friends" || code == "friends.outgoing_request_pending" || code == "friends.incoming_request_pending" || code == "friends.pair_cooldown" || code == "friends.friend_limit" || code == "friends.outgoing_limit" || code == "friends.send_cooldown" || code == "friends.request_unavailable" || code == "friends.request_expired" || code == "friends.not_friends");
        private static string SafeCode(string code) => !string.IsNullOrEmpty(code) && code.Length <= 96 && code.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '.') ? code : "friends.unavailable";
    }
    public sealed class FriendsService : IDisposable
    {
        private const string Pending = "pending-command";
        private readonly ScopedOwnerContext owner;
        private Guid playerId;
        private readonly StorageScope scope;
        private readonly IFriendsRemote? remote;
        private readonly IFriendsStateCodec codec;
        private readonly IDurableFeatureStateStore state;
        private readonly ISerializedStorageExecutor transactions;
        private readonly Func<long> now;
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly object gate = new object();
        private bool disposed;
        private string Namespace => FriendsCache.Namespace(playerId);
        public FriendsService(ScopedOwnerContext owner, Guid playerId, IFriendsRemote? remote, IFriendsStateCodec codec, IDurableFeatureStateStore state, ISerializedStorageExecutor transactions, Func<long>? nowMilliseconds = null)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.");
            this.owner = owner; this.playerId = playerId; this.remote = remote;
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec)); this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            now = nowMilliseconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            scope = new StorageScope(owner.Owner.Backend.Value, new PlatformId(owner.Owner.AppId.ToString()), new PlatformId(owner.Owner.UserId.ToString()));
        }
        public FriendsService(ScopedOwnerContext owner, IFriendsRemote? remote, IFriendsStateCodec codec, IDurableFeatureStateStore state, ISerializedStorageExecutor transactions, Func<long>? nowMilliseconds = null)
            : this(owner, Guid.Empty, remote, codec, state, transactions, nowMilliseconds) { }
        public Guid PlayerId { get { lock (gate) return playerId; } }
        public async Task<FeatureSnapshot<FriendsResponse>> ReadCachedAsync(ScopedOwnerContext requestedOwner, FriendsQuery query, CancellationToken token)
        {
            Ensure(requestedOwner); if (query == null) throw new ArgumentNullException(nameof(query));
            if (!Cacheable(query)) return Missing();
            using var linked = Link(token);
            long expectedEpoch = -1;
            var result = await transactions.ExecuteAsync(scope, tx =>
            {
                lock (gate)
                {
                    Ensure(requestedOwner); linked.Token.ThrowIfCancellationRequested();
                    playerId = FriendsPlayerBinding.Load(tx, state, owner, playerId);
                    if (playerId == Guid.Empty) return Missing();
                    var row = state.Read(tx, owner, Namespace, query.View);
                    if (row == null || !FriendsCache.Matches(row, Context(query))) return Missing();
                    var value = codec.DecodeResponse(row.CopyPayload()); Validate(value, query, null);
                    var fence = state.Read(tx, owner, FriendsPlayerBinding.Namespace, FriendsCache.Epoch);
                    expectedEpoch = fence?.Revision ?? 0;
                    if (value.Revision < FriendsCache.Floor(fence) || value.GraphRevision < FriendsCache.GraphFloor(fence)) return Missing();
                    long clock = Now();
                    if (clock >= row.ConfirmedAtMilliseconds && clock - row.ConfirmedAtMilliseconds >= 604800000) { state.Delete(tx, owner, Namespace, query.View); return Missing(); }
                    return Snapshot(value, FriendsCache.Fresh(row.ConfirmedAtMilliseconds, clock, value.ServerTimeMilliseconds, value.CacheExpiresAtMilliseconds) ? SnapshotFreshness.Current : SnapshotFreshness.Stale, row.ConfirmedAtMilliseconds);
                }
            }, linked.Token).ConfigureAwait(false);
            if (result.Value != null) await CheckFenceAsync(expectedEpoch, linked.Token).ConfigureAwait(false);
            Ensure(requestedOwner); linked.Token.ThrowIfCancellationRequested(); return result;
        }
        public async Task<FeatureSnapshot<FriendsResponse>> ReadAsync(ScopedOwnerContext requestedOwner, FriendsQuery query, CancellationToken token)
        {
            var cached = await ReadCachedAsync(requestedOwner, query, token).ConfigureAwait(false);
            if (cached.Freshness == SnapshotFreshness.Current || remote == null) return cached;
            try { return await QueryAsync(requestedOwner, query, token).ConfigureAwait(false); }
            catch (FriendsOperationException error) when (cached.Value != null && (error.Failure.Kind == RemoteFailureKind.Dependency || error.Failure.Kind == RemoteFailureKind.Unavailable || error.Failure.Kind == RemoteFailureKind.NotSent || error.Failure.Kind == RemoteFailureKind.RateLimited))
            { return await ReadCachedAsync(requestedOwner, query, token).ConfigureAwait(false); }
        }
        public async Task<FeatureSnapshot<FriendsResponse>> QueryAsync(ScopedOwnerContext requestedOwner, FriendsQuery query, CancellationToken token)
        {
            Ensure(requestedOwner); if (query == null) throw new ArgumentNullException(nameof(query));
            using var linked = Link(token); await serial.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                if (remote == null) throw Error(RemoteFailureKind.Unavailable, "friends.offline");
                long start = Now(); var before = await state.ReadAsync(owner, FriendsPlayerBinding.Namespace, FriendsCache.Epoch, linked.Token).ConfigureAwait(false);
                RemoteResult<FriendsResponse> result;
                try { result = await remote.QueryAsync(query, linked.Token).ConfigureAwait(false); }
                catch (FriendsOperationException error) { if (error.Failure.Kind == RemoteFailureKind.Authorization || error.Failure.Kind == RemoteFailureKind.Authentication) await InvalidateAsync(linked.Token).ConfigureAwait(false); throw; }
                Ensure(requestedOwner); linked.Token.ThrowIfCancellationRequested();
                if (!result.IsSuccess) { if (result.Failure.Kind == RemoteFailureKind.Authorization || result.Failure.Kind == RemoteFailureKind.Authentication) await InvalidateAsync(linked.Token).ConfigureAwait(false); throw new FriendsOperationException(result.Failure, "friends.unavailable"); }
                var value = result.Value!; Validate(value, query, null);
                var committed = await transactions.ExecuteAsync(scope, tx =>
                {
                    lock (gate)
                    {
                        Ensure(requestedOwner); linked.Token.ThrowIfCancellationRequested(); var fence = state.Read(tx, owner, FriendsPlayerBinding.Namespace, FriendsCache.Epoch);
                        FriendsCache.CheckEpoch(fence, before?.Revision ?? 0);
                        var bound = FriendsPlayerBinding.Bind(tx, state, owner, playerId, value.PlayerId);
                        if (value.Revision < FriendsCache.Floor(fence) || value.GraphRevision < FriendsCache.GraphFloor(fence)) throw Error(RemoteFailureKind.Protocol, "friends.revision_regressed");
                        if (value.Revision > FriendsCache.Floor(fence) || value.GraphRevision > FriendsCache.GraphFloor(fence)) FriendsCache.Invalidate(tx, state, owner, bound, value.Revision, value.GraphRevision);
                        string ns = FriendsCache.Namespace(bound);
                        if (Cacheable(query)) { var row = state.Read(tx, owner, ns, query.View); state.Upsert(tx, new DurableFeatureMutation(owner, ns, query.View, checked((row?.Revision ?? 0) + 1), start, codec.EncodeResponse(value), FriendsCache.Context(Context(query)))); }
                        return (Player: bound, Epoch: state.Read(tx, owner, FriendsPlayerBinding.Namespace, FriendsCache.Epoch)?.Revision ?? 0);
                    }
                }, linked.Token).ConfigureAwait(false);
                await CheckFenceAsync(committed.Epoch, linked.Token).ConfigureAwait(false);
                lock (gate) { Ensure(requestedOwner); linked.Token.ThrowIfCancellationRequested(); playerId = committed.Player; }
                return Snapshot(value, FriendsCache.Fresh(start, Now(), value.ServerTimeMilliseconds, value.CacheExpiresAtMilliseconds) ? SnapshotFreshness.Current : SnapshotFreshness.Stale, start);
            }
            finally { serial.Release(); }
        }
        public async Task<FriendsCommand?> ReadPendingAsync(ScopedOwnerContext requestedOwner, CancellationToken token)
        {
            Ensure(requestedOwner); using var linked = Link(token);
            var value = await transactions.ExecuteAsync(scope, tx => { lock (gate) { Ensure(requestedOwner); linked.Token.ThrowIfCancellationRequested(); playerId = FriendsPlayerBinding.Load(tx, state, owner, playerId); if (playerId == Guid.Empty) return null; var row = state.Read(tx, owner, Namespace, Pending); return row == null ? null : codec.DecodeCommand(row.CopyPayload()); } }, linked.Token).ConfigureAwait(false);
            Ensure(requestedOwner); linked.Token.ThrowIfCancellationRequested(); return value;
        }
        public async Task<FeatureSnapshot<FriendsResponse>> ExecuteAsync(ScopedOwnerContext requestedOwner, FriendsCommand command, CancellationToken token)
        {
            Ensure(requestedOwner); if (command == null) throw new ArgumentNullException(nameof(command));
            if (remote == null) throw Error(RemoteFailureKind.Unavailable, "friends.offline"); var bytes = codec.EncodeCommand(command);
            using var linked = Link(token); await serial.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                await transactions.ExecuteAsync(scope, tx => { lock (gate) { Ensure(requestedOwner); linked.Token.ThrowIfCancellationRequested(); playerId = FriendsPlayerBinding.Load(tx, state, owner, playerId); if (playerId == Guid.Empty) throw Error(RemoteFailureKind.Unavailable, "friends.player_unbound"); FriendsPlayerBinding.Bind(tx, state, owner, playerId, playerId); var row = state.Read(tx, owner, Namespace, Pending); if (row != null && !row.CopyPayload().SequenceEqual(bytes)) throw Error(RemoteFailureKind.Conflict, "friends.pending_command"); if (row == null) state.Upsert(tx, new DurableFeatureMutation(owner, Namespace, Pending, 0, Now(), bytes, Array.Empty<byte>())); return true; } }, linked.Token).ConfigureAwait(false);
                RemoteResult<FriendsResponse> result;
                try { result = await remote.ExecuteAsync(command, linked.Token).ConfigureAwait(false); }
                catch (FriendsOperationException error)
                {
                    Ensure(requestedOwner); linked.Token.ThrowIfCancellationRequested();
                    if (error.RetainedOperationId == command.OperationId.Value) await ResolveAsync(bytes, linked.Token).ConfigureAwait(false);
                    else if (error.Failure.Kind == RemoteFailureKind.Authorization || error.Failure.Kind == RemoteFailureKind.Authentication) await InvalidateAsync(linked.Token).ConfigureAwait(false);
                    throw;
                }
                Ensure(requestedOwner); linked.Token.ThrowIfCancellationRequested();
                if (!result.IsSuccess) { if (result.Failure.Kind == RemoteFailureKind.Authorization || result.Failure.Kind == RemoteFailureKind.Authentication) await InvalidateAsync(linked.Token).ConfigureAwait(false); throw new FriendsOperationException(result.Failure, "friends.unavailable"); }
                var value = result.Value!; Validate(value, null, command);
                // Retained receipts intentionally bypass query revision and time floors.
                await ResolveAsync(bytes, linked.Token, value.Revision, value.GraphRevision).ConfigureAwait(false); Ensure(requestedOwner); linked.Token.ThrowIfCancellationRequested();
                return Snapshot(value, SnapshotFreshness.Current);
            }
            finally { serial.Release(); }
        }
        private Task<bool> ResolveAsync(byte[] expected, CancellationToken token, long revision = 0, long graphRevision = 0) => transactions.ExecuteAsync(scope, tx => { lock (gate) { Ensure(owner); token.ThrowIfCancellationRequested(); var row = state.Read(tx, owner, Namespace, Pending); if (row == null || !row.CopyPayload().SequenceEqual(expected)) throw Error(RemoteFailureKind.Conflict, "friends.pending_mismatch"); state.Delete(tx, owner, Namespace, Pending); FriendsCache.Invalidate(tx, state, owner, playerId, revision, graphRevision); return true; } }, token);
        private Task<bool> InvalidateAsync(CancellationToken token) => transactions.ExecuteAsync(scope, tx => { lock (gate) { Ensure(owner); token.ThrowIfCancellationRequested(); playerId = FriendsPlayerBinding.Load(tx, state, owner, playerId); FriendsCache.InvalidateOwner(tx, state, owner); return true; } }, token);
        private async Task CheckFenceAsync(long expected, CancellationToken token) { var fence = await state.ReadAsync(owner, FriendsPlayerBinding.Namespace, FriendsCache.Epoch, token).ConfigureAwait(false); Ensure(owner); token.ThrowIfCancellationRequested(); FriendsCache.CheckEpoch(fence, expected); }
        public static void InvalidateCache(ILocalStorageTransaction tx, IDurableFeatureStateStore state, ScopedOwnerContext owner, Guid playerId, long requiredRevision = 0) { if (tx == null || state == null) throw new ArgumentNullException(); FriendsCache.Invalidate(tx, state, owner, playerId, requiredRevision); }
        public static void InvalidateCache(ILocalStorageTransaction tx, IDurableFeatureStateStore state, ScopedOwnerContext owner) { if (tx == null || state == null) throw new ArgumentNullException(); FriendsCache.InvalidateOwner(tx, state, owner); }
        private void Validate(FriendsResponse value, FriendsQuery? query, FriendsCommand? command)
        {
            if (value == null || value.AppId != owner.Owner.AppId.Value || value.AccountId != owner.Owner.UserId.Value || playerId != Guid.Empty && value.PlayerId != playerId || value.OperationId != command?.OperationId.Value || value.View != query?.View || query != null && (value.Friends.Count > query.PageSize || value.Requests.Count > query.PageSize || value.BlockedPlayers.Count > query.PageSize || value.Suggestions.Count > query.PageSize) || command != null && value.ResultCode != command.Action) throw Error(RemoteFailureKind.Protocol, "friends.context_mismatch");
            if (command?.Payload.TargetPlayerId is Guid target && value.Player?.PlayerId != target || command?.Payload.RequestId is Guid request && value.Request?.RequestId != request) throw Error(RemoteFailureKind.Protocol, "friends.command_mismatch");
            if (command != null && (command.Action == "set_privacy" && value.AllowRequests != command.Payload.AllowRequests || new[] { "send", "accept", "decline", "cancel" }.Contains(command.Action) && (value.Request == null || value.Request.SenderPlayerId != value.PlayerId && value.Request.RecipientPlayerId != value.PlayerId))) throw Error(RemoteFailureKind.Protocol, "friends.command_mismatch");
        }
        private static bool Cacheable(FriendsQuery query) => query.Cursor == null && query.PageSize == 30;
        private static string Context(FriendsQuery query) => query.View + ":" + query.PageSize + ":" + query.PublicCode;
        private FeatureSnapshot<FriendsResponse> Missing() => new FeatureSnapshot<FriendsResponse>(owner, 0, SnapshotFreshness.Missing, null);
        private FeatureSnapshot<FriendsResponse> Snapshot(FriendsResponse value, SnapshotFreshness freshness, long? localRequestStarted = null) => new FeatureSnapshot<FriendsResponse>(owner, value.Revision, freshness == SnapshotFreshness.Current ? FeatureSnapshotState.Available : FeatureSnapshotState.Stale, freshness, value, localRequestStarted, null);
        private long Now() { long value = now(); FriendsValidation.Time(value); return value; }
        private static FriendsOperationException Error(RemoteFailureKind kind, string code) => new FriendsOperationException(new RemoteFailure(kind), code);
        private CancellationTokenSource Link(CancellationToken token) { lock (gate) { Ensure(owner); return CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); } }
        private void Ensure(ScopedOwnerContext requestedOwner) { lock (gate) { if (disposed) throw new ObjectDisposedException(nameof(FriendsService)); if (requestedOwner != owner) throw Error(RemoteFailureKind.Authorization, "friends.owner_mismatch"); } }
        public void Dispose() { lock (gate) { if (disposed) return; disposed = true; } lifetime.Cancel(); }
    }
}
