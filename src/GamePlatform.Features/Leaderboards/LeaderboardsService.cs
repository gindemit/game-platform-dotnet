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
namespace GamePlatform.Features.Leaderboards
{
    public interface ILeaderboardsRemote
    {
        Task<RemoteResult<LeaderboardsResponse>> QueryAsync(LeaderboardsQuery query, CancellationToken cancellationToken);
        Task<RemoteResult<LeaderboardsCountryResponse>> SetCountryAsync(LeaderboardsCountryCommand command, CancellationToken cancellationToken);
    }
    public interface ILeaderboardsStateCodec
    {
        byte[] EncodeResponse(LeaderboardsResponse value);
        LeaderboardsResponse DecodeResponse(byte[] bytes);
        byte[] EncodeCountry(LeaderboardsCountryCommand value);
        LeaderboardsCountryCommand DecodeCountry(byte[] bytes);
    }
    public sealed class LeaderboardsOperationException : InvalidOperationException
    {
        public LeaderboardsOperationException(RemoteFailure failure, string resultCode)
            : base("The leaderboard operation could not be confirmed: " + SafeCode(resultCode))
        { Failure = failure; ResultCode = SafeCode(resultCode); }
        public RemoteFailure Failure { get; }
        public string ResultCode { get; }
        private static string SafeCode(string value) => !string.IsNullOrEmpty(value) && value.Length <= 96 &&
            value.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '.') ? value : "leaderboards_unavailable";
    }
    /// <summary>Account-owned standings reads and explicit durable country retries; never submits scores.</summary>
    public sealed class LeaderboardsService : IDisposable
    {
        private const string Namespace = "leaderboards";
        private const string Pending = "pending-country";
        private static readonly string[] Boards = { "players-monthly", "teams-monthly", "players-daily", "teams-daily", "players-weekly", "teams-weekly" };
        private readonly ScopedOwnerContext owner;
        private readonly StorageScope storageScope;
        private readonly ILeaderboardsRemote? remote;
        private readonly ILeaderboardsStateCodec codec;
        private readonly IDurableFeatureStateStore state;
        private readonly ISerializedStorageExecutor transactions;
        private readonly Func<long> nowMilliseconds;
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly object gate = new object();
        private bool disposed;
        public LeaderboardsService(ScopedOwnerContext owner, ILeaderboardsRemote? remote, ILeaderboardsStateCodec codec,
            IDurableFeatureStateStore state, ISerializedStorageExecutor transactions, Func<long> nowMilliseconds)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            this.owner = owner; storageScope = new StorageScope(owner.Owner.Backend.Value, new PlatformId(owner.Owner.AppId.ToString()), new PlatformId(owner.Owner.UserId.ToString()));
            this.remote = remote; this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.state = state ?? throw new ArgumentNullException(nameof(state)); this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            this.nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
        }
        public async Task<FeatureSnapshot<LeaderboardsResponse>> ReadCachedAsync(ScopedOwnerContext requestedOwner, LeaderboardsQuery query, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner); if (query == null) throw new ArgumentNullException(nameof(query));
            string? key = CacheKey(query); if (key == null) return Missing();
            using var linked = Link(cancellationToken);
            var cached = await state.ReadAsync(owner, Namespace, key, linked.Token).ConfigureAwait(false);
            EnsureOwner(requestedOwner); linked.Token.ThrowIfCancellationRequested();
            if (cached == null) return Missing();
            var value = codec.DecodeResponse(cached.CopyPayload());
            // Fixed slots stay bounded even when callers browse arbitrary country cohorts.
            if (value.CountryCode != query.CountryCode || value.BoardId != query.BoardId || value.Scope != query.Scope || value.Entries.Count > query.Limit) return Missing();
            return Snapshot(value, SnapshotFreshness.Stale);
        }
        public async Task<LeaderboardsCountryCommand?> ReadPendingAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner); using var linked = Link(cancellationToken);
            var value = await state.ReadAsync(owner, Namespace, Pending, linked.Token).ConfigureAwait(false);
            EnsureOwner(requestedOwner); linked.Token.ThrowIfCancellationRequested();
            return value == null ? null : codec.DecodeCountry(value.CopyPayload());
        }
        public async Task<FeatureSnapshot<LeaderboardsResponse>> QueryAsync(ScopedOwnerContext requestedOwner, LeaderboardsQuery query, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner); if (query == null) throw new ArgumentNullException(nameof(query));
            using var linked = Link(cancellationToken); await serial.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                EnsureOwner(requestedOwner); if (remote == null) throw Error(RemoteFailureKind.Unavailable, "leaderboards_offline");
                RemoteResult<LeaderboardsResponse> result;
                try { result = await remote.QueryAsync(query, linked.Token).ConfigureAwait(false); }
                catch (LeaderboardsOperationException error)
                { if (error.Failure.Kind == RemoteFailureKind.Authorization) await InvalidateAsync(linked.Token).ConfigureAwait(false); throw; }
                EnsureOwner(requestedOwner); linked.Token.ThrowIfCancellationRequested();
                if (!result.IsSuccess)
                {
                    if (result.Failure.Kind == RemoteFailureKind.Authorization) await InvalidateAsync(linked.Token).ConfigureAwait(false);
                    throw new LeaderboardsOperationException(result.Failure, "leaderboards_" + result.Failure.Kind.ToString().ToLowerInvariant());
                }
                var value = result.Value!; ValidateQuery(query, value);
                string? key = CacheKey(query);
                if (key != null)
                {
                    var bytes = codec.EncodeResponse(value);
                    await transactions.ExecuteAsync(storageScope, tx =>
                    {
                        lock (gate)
                        {
                            EnsureOwner(requestedOwner); linked.Token.ThrowIfCancellationRequested();
                            var prior = state.Read(tx, owner, Namespace, key);
                            state.Upsert(tx, new DurableFeatureMutation(owner, Namespace, key, checked((prior?.Revision ?? 0) + 1), Now(), bytes, Array.Empty<byte>()));
                            return true;
                        }
                    }, linked.Token).ConfigureAwait(false);
                }
                EnsureOwner(requestedOwner); linked.Token.ThrowIfCancellationRequested(); return Snapshot(value, SnapshotFreshness.Current);
            }
            finally { serial.Release(); }
        }
        public async Task<LeaderboardsCountryResponse> SetCountryAsync(ScopedOwnerContext requestedOwner, LeaderboardsCountryCommand command, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner); if (command == null) throw new ArgumentNullException(nameof(command));
            if (remote == null) throw Error(RemoteFailureKind.Unavailable, "leaderboards_offline");
            byte[] bytes = codec.EncodeCountry(command);
            using var linked = Link(cancellationToken); await serial.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                await transactions.ExecuteAsync(storageScope, tx =>
                {
                    lock (gate)
                    {
                        EnsureOwner(requestedOwner); linked.Token.ThrowIfCancellationRequested();
                        var previous = state.Read(tx, owner, Namespace, Pending);
                        if (previous != null && !previous.CopyPayload().SequenceEqual(bytes)) throw Error(RemoteFailureKind.Conflict, "leaderboards_pending_country");
                        if (previous == null) state.Upsert(tx, new DurableFeatureMutation(owner, Namespace, Pending, 0, Now(), bytes, Array.Empty<byte>()));
                        return true;
                    }
                }, linked.Token).ConfigureAwait(false);
                RemoteResult<LeaderboardsCountryResponse> result;
                try { result = await remote.SetCountryAsync(command, linked.Token).ConfigureAwait(false); }
                catch (LeaderboardsOperationException error)
                {
                    // Owner access may disappear before an already committed receipt can be read.
                    if (error.Failure.Kind == RemoteFailureKind.Authorization) await InvalidateAsync(linked.Token).ConfigureAwait(false);
                    if (DefiniteRejection(error))
                        await ClearPendingAsync(linked.Token).ConfigureAwait(false);
                    throw;
                }
                EnsureOwner(requestedOwner); linked.Token.ThrowIfCancellationRequested();
                if (!result.IsSuccess)
                {
                    if (result.Failure.Kind == RemoteFailureKind.Authorization) await InvalidateAsync(linked.Token).ConfigureAwait(false);
                    throw new LeaderboardsOperationException(result.Failure, "leaderboards_" + result.Failure.Kind.ToString().ToLowerInvariant());
                }
                var value = result.Value!; ValidateCountry(command, value);
                await transactions.ExecuteAsync(storageScope, tx =>
                {
                    lock (gate)
                    {
                        EnsureOwner(requestedOwner); linked.Token.ThrowIfCancellationRequested();
                        state.Delete(tx, owner, Namespace, Pending);
                        state.Delete(tx, owner, Namespace, command.BoardId + ":global");
                        state.Delete(tx, owner, Namespace, command.BoardId + ":country");
                        return true;
                    }
                }, linked.Token).ConfigureAwait(false);
                EnsureOwner(requestedOwner); linked.Token.ThrowIfCancellationRequested(); return value;
            }
            finally { serial.Release(); }
        }
        private Task<bool> ClearPendingAsync(CancellationToken token) => transactions.ExecuteAsync(storageScope, tx =>
        { lock (gate) { EnsureOwner(owner); token.ThrowIfCancellationRequested(); state.Delete(tx, owner, Namespace, Pending); return true; } }, token);
        private Task<bool> InvalidateAsync(CancellationToken token) => transactions.ExecuteAsync(storageScope, tx =>
        {
            lock (gate)
            {
                EnsureOwner(owner); token.ThrowIfCancellationRequested();
                foreach (string board in Boards) foreach (string scope in new[] { "global", "country" }) state.Delete(tx, owner, Namespace, board + ":" + scope);
                return true;
            }
        }, token);
        internal static void ValidateQuery(LeaderboardsQuery query, LeaderboardsResponse value)
        {
            if (value == null || value.BoardId != query.BoardId || value.Scope != query.Scope || value.CountryCode != query.CountryCode || value.Entries.Count > query.Limit ||
                query.Cursor == null && value.Entries.Count > 0 && value.Entries[0].Rank != 1 || value.ExpiresAt <= value.ServerTime)
                throw Error(RemoteFailureKind.Protocol, "leaderboards_query_mismatch");
        }
        internal static void ValidateCountry(LeaderboardsCountryCommand command, LeaderboardsCountryResponse value)
        {
            if (value == null || value.OperationId != command.OperationId.Value || value.BoardId != command.BoardId ||
                value.ParticipantKind != command.ParticipantKind || value.CountryCode != command.CountryCode || command.TeamId.HasValue && value.ParticipantId != command.TeamId.Value)
                throw Error(RemoteFailureKind.Protocol, "leaderboards_country_mismatch");
        }
        private static string? CacheKey(LeaderboardsQuery query) => query.Cursor == null && query.Limit == 30 && Boards.Contains(query.BoardId) ? query.BoardId + ":" + query.Scope : null;
        // A coarse transport/gateway failure cannot prove that an earlier ambiguous attempt did not commit.
        // Only a separately reviewed backend rejection code may release this immutable journal entry.
        private static bool DefiniteRejection(LeaderboardsOperationException error) => error.Failure.Kind == RemoteFailureKind.Validation &&
            error.ResultCode == "leaderboards.invalid_country";
        private static LeaderboardsOperationException Error(RemoteFailureKind kind, string code) => new LeaderboardsOperationException(new RemoteFailure(kind), code);
        private long Now() { long value = nowMilliseconds(); if (value < 0 || value > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(nowMilliseconds)); return value; }
        private FeatureSnapshot<LeaderboardsResponse> Missing() => new FeatureSnapshot<LeaderboardsResponse>(owner, 0, SnapshotFreshness.Missing, null);
        // No server projection revision exists in this read protocol; never fabricate one from score or time.
        private FeatureSnapshot<LeaderboardsResponse> Snapshot(LeaderboardsResponse value, SnapshotFreshness freshness) => new FeatureSnapshot<LeaderboardsResponse>(owner, 0, freshness, value);
        private CancellationTokenSource Link(CancellationToken token) { lock (gate) { EnsureOwner(owner); return CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); } }
        private void EnsureOwner(ScopedOwnerContext value)
        { lock (gate) { if (disposed) throw new ObjectDisposedException(nameof(LeaderboardsService)); if (value != owner) throw Error(RemoteFailureKind.Authorization, "leaderboards_owner_mismatch"); } }
        public void Dispose() { lock (gate) { if (disposed) return; disposed = true; } lifetime.Cancel(); }
    }
}
