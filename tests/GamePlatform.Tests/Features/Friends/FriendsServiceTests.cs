using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Friends;
using GamePlatform.Features.Leaderboards;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Transport.Http;
using T = GamePlatform.Tests.Features.Teams.TeamsServiceTests;
namespace GamePlatform.Tests.Features.Friends
{
    public sealed class FriendsServiceTests
    {
        private const long Start = 1790000000000;
        private static FriendsResponse Response(string? view = "mine", Guid? operation = null, long revision = 0, Guid? player = null, long time = Start, bool allow = true) => new FriendsResponse(T.App, T.Account, player ?? T.Player, time, time + 60000, operation, view, revision, 0, "01ABCDEFGHJK", allow, 0, 0, 0, Array.Empty<Friend>(), Array.Empty<FriendRequest>(), Array.Empty<BlockedPlayer>(), Array.Empty<PublicPlayer>(), null, null, null, null, null, operation.HasValue ? "set_privacy" : null);
        private static FriendsCommand Command(Guid? operation = null, bool allow = false) => new FriendsCommand(new OperationId(operation ?? T.Operation), "set_privacy", new FriendsCommandPayload(allowRequests: allow));
        private static FriendsService Service(T.Database db, Remote? remote, Func<long>? clock = null, Guid? player = null, long generation = 1) => player.HasValue ? new FriendsService(T.Owner(generation), player.Value, remote, new FriendsStateCodec(), db.Store, db.Current, clock ?? (() => Start)) : new FriendsService(T.Owner(generation), remote, new FriendsStateCodec(), db.Store, db.Current, clock ?? (() => Start));
        private static LeaderboardsResponse Ranking(Guid? player = null, long graph = 0)
        {
            var id = player ?? T.Player;
            var self = new LeaderboardEntry(id, 1, 0, "Bloom");
            return new LeaderboardsResponse("players-monthly", "player", "friends", null, "2026-10", Start - 100000, Start + 1000000, Start + 1000000, T.Team, Start, Start + 300000, "client_trusted_unvalidated", new[] { self }, null, self, 201, Start, null, T.App, T.Account, id, graph, 1);
        }
        [Fact]
        public async Task FreshRealSqliteRestartUsesNoNetworkAndExpiredOfflineIsStale()
        {
            await using var db = await T.Database.Open();
            long now = Start;
            var remote = new Remote();
            using (var service = Service(db, remote, () => now))
                await service.ReadAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            await db.Reopen();
            now += 10000;
            using var reopened = Service(db, remote, () => now, generation: 2);
            Assert.Equal(SnapshotFreshness.Current, (await reopened.ReadAsync(T.Owner(2), new FriendsQuery("mine"), CancellationToken.None)).Freshness);
            Assert.Equal(1, remote.Queries);
            using var offline = Service(db, null, () => Start + 60001, generation: 3);
            Assert.Equal(SnapshotFreshness.Stale, (await offline.ReadAsync(T.Owner(3), new FriendsQuery("mine"), CancellationToken.None)).Freshness);
            Assert.Equal(SnapshotFreshness.Stale, (await Service(db, null, () => Start - 1, generation: 4).ReadCachedAsync(T.Owner(4), new FriendsQuery("mine"), CancellationToken.None)).Freshness);
        }
        [Fact]
        public async Task UnboundOfflineCannotMutateOrInventPlayer()
        {
            await using var db = await T.Database.Open();
            using var offline = Service(db, null);
            Assert.Equal(SnapshotFreshness.Missing, (await offline.ReadAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None)).Freshness);
            Assert.Equal(Guid.Empty, offline.PlayerId);
            using var online = Service(db, new Remote());
            Assert.Equal("friends.player_unbound", (await Assert.ThrowsAsync<FriendsOperationException>(() => online.ExecuteAsync(T.Owner(), Command(), CancellationToken.None))).ResultCode);
            Assert.Null(await online.ReadPendingAsync(T.Owner(), CancellationToken.None));
        }

        [Fact]
        public async Task ReopenedCurrentCacheExposesOriginalAgeAnchorWithoutExtendingServerExpiry()
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            long now = Start;
            using (var first = Service(db, remote, () => now))
                await first.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            await db.Reopen();
            now += 59000;
            using var reopened = Service(db, remote, () => now, generation: 2);
            var cached = await reopened.ReadAsync(T.Owner(2), new FriendsQuery("mine"), CancellationToken.None);
            Assert.Equal(SnapshotFreshness.Current, cached.Freshness);
            Assert.Equal(Start, cached.LastConfirmedAtMilliseconds);
            Assert.Equal(Start, cached.Value!.ServerTimeMilliseconds);
            Assert.Equal(1000, cached.Value.CacheExpiresAtMilliseconds - (cached.Value.ServerTimeMilliseconds + now - cached.LastConfirmedAtMilliseconds!.Value));
            now += 1001;
            Assert.Equal(SnapshotFreshness.Stale, (await reopened.ReadCachedAsync(T.Owner(2), new FriendsQuery("mine"), CancellationToken.None)).Freshness);
            Assert.Equal(1, remote.Queries);
        }
        [Fact]
        public async Task RankFirstBindsSamePlayerAndFreshRankRestartAvoidsNetwork()
        {
            await using var db = await T.Database.Open();
            var remote = new RankRemote();
            using (var ranks = new LeaderboardsService(T.Owner(), remote, new LeaderboardsStateCodec(), db.Store, db.Current, () => Start))
                await ranks.ReadAsync(T.Owner(), new LeaderboardsQuery("players-monthly", "friends"), CancellationToken.None);
            using var friends = Service(db, new Remote());
            await friends.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            Assert.Equal(T.Player, friends.PlayerId);
            await db.Reopen();
            using var reopened = new LeaderboardsService(T.Owner(2), remote, new LeaderboardsStateCodec(), db.Store, db.Current, () => Start + 1000);
            Assert.Equal(0, (await reopened.ReadAsync(T.Owner(2), new LeaderboardsQuery("players-monthly", "friends"), CancellationToken.None)).Value!.Self!.Score);
            Assert.Equal(1, remote.Queries);
        }
        [Fact]
        public async Task ConflictingAuthenticatedPlayerCannotReplaceBindingOrJournal()
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            using var service = Service(db, remote);
            await service.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            remote.Command = () => Task.FromResult(RemoteResult<FriendsResponse>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)));
            await Assert.ThrowsAsync<FriendsOperationException>(() => service.ExecuteAsync(T.Owner(), Command(), CancellationToken.None));
            using var foreign = Service(db, new Remote { Read = q => Task.FromResult(RemoteResult<FriendsResponse>.Success(Response(q.View, player: T.Team))) });
            await Assert.ThrowsAsync<FriendsOperationException>(() => foreign.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None));
            Assert.NotNull(await service.ReadPendingAsync(T.Owner(), CancellationToken.None));
            using var explicitForeign = Service(db, null, player: T.Team);
            await Assert.ThrowsAsync<FriendsOperationException>(() => explicitForeign.ReadCachedAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None));
        }

        [Fact]
        public async Task SharedSqliteRejectsForeignBackendAccountAppAndSeparateViewsNeverInheritCache()
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            using var service = Service(db, remote);
            await service.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            var original = T.Owner();
            var separate = new ScopedOwnerContext(original.Owner, new SemanticId("separate"), 1);
            using var otherView = new FriendsService(separate, null, new FriendsStateCodec(), db.Store, db.Current, () => Start);
            Assert.Equal(SnapshotFreshness.Missing, (await otherView.ReadCachedAsync(separate, new FriendsQuery("mine"), CancellationToken.None)).Freshness);
            foreach (var foreign in new[] { new OwnerScope(new BackendNamespace("other"), original.Owner.AppId, original.Owner.UserId), new OwnerScope(original.Owner.Backend, new AppId(T.Team), original.Owner.UserId), new OwnerScope(original.Owner.Backend, original.Owner.AppId, new PlatformUserId(T.Team)) })
            {
                var context = new ScopedOwnerContext(foreign, original.ViewKey, 1);
                using var fenced = new FriendsService(context, null, new FriendsStateCodec(), db.Store, db.Current, () => Start);
                await Assert.ThrowsAsync<StorageException>(() => fenced.ReadCachedAsync(context, new FriendsQuery("mine"), CancellationToken.None));
            }
        }

        [Fact]
        public async Task ResetInvalidationClearsRanksAndViewsButPreservesPendingAndUnboundEpoch()
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            using var service = Service(db, remote);
            await db.Current.ExecuteAsync(db.Scope, tx => { FriendsService.InvalidateCache(tx, db.Store, T.Owner()); return true; }, CancellationToken.None);
            await service.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            using var ranks = new LeaderboardsService(T.Owner(), new RankRemote(), new LeaderboardsStateCodec(), db.Store, db.Current, () => Start);
            await ranks.QueryAsync(T.Owner(), new LeaderboardsQuery("players-monthly", "friends"), CancellationToken.None);
            remote.Command = () => Task.FromResult(RemoteResult<FriendsResponse>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)));
            await Assert.ThrowsAsync<FriendsOperationException>(() => service.ExecuteAsync(T.Owner(), Command(), CancellationToken.None));
            await db.Current.ExecuteAsync(db.Scope, tx => { FriendsService.InvalidateCache(tx, db.Store, T.Owner()); return true; }, CancellationToken.None);
            Assert.Null((await service.ReadCachedAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None)).Value);
            Assert.Null((await ranks.ReadCachedAsync(T.Owner(), new LeaderboardsQuery("players-monthly", "friends"), CancellationToken.None)).Value);
            Assert.NotNull(await service.ReadPendingAsync(T.Owner(), CancellationToken.None));
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InvalidationFencesLateFirstAndContinuationReplies(bool continuation)
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            using var service = Service(db, remote);
            if (continuation)
                await service.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            var reached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reply = new TaskCompletionSource<RemoteResult<FriendsResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            remote.Read = q => { reached.SetResult(true); return reply.Task; };
            var pending = service.QueryAsync(T.Owner(), new FriendsQuery(continuation ? "friends" : "mine", cursor: continuation ? "AAAAAAAAAAAAAAAA" : null), CancellationToken.None);
            await reached.Task;
            await db.Current.ExecuteAsync(db.Scope, tx => { FriendsService.InvalidateCache(tx, db.Store, T.Owner(), T.Player, 1); return true; }, CancellationToken.None);
            reply.SetResult(RemoteResult<FriendsResponse>.Success(Response(continuation ? "friends" : "mine", revision: 1)));
            Assert.Equal("friends.refresh_required", (await Assert.ThrowsAsync<FriendsOperationException>(() => pending)).ResultCode);
            Assert.Null((await service.ReadCachedAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None)).Value);
        }
        [Fact]
        public async Task LostReceiptRestartPreservesExactBodyAndHistoricalSuccessResolvesAboveQueryFloor()
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            using (var first = Service(db, remote))
            {
                await first.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
                remote.Command = () => Task.FromResult(RemoteResult<FriendsResponse>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)));
                await Assert.ThrowsAsync<FriendsOperationException>(() => first.ExecuteAsync(T.Owner(), Command(), CancellationToken.None));
            }
            await db.Reopen();
            using var resumed = Service(db, remote, generation: 2);
            var pending = await resumed.ReadPendingAsync(T.Owner(2), CancellationToken.None);
            Assert.Equal(new FriendsStateCodec().EncodeCommand(Command()), new FriendsStateCodec().EncodeCommand(pending!));
            await db.Current.ExecuteAsync(db.Scope, tx => { FriendsService.InvalidateCache(tx, db.Store, T.Owner(2), T.Player, 20); return true; }, CancellationToken.None);
            await Assert.ThrowsAsync<FriendsOperationException>(() => resumed.ExecuteAsync(T.Owner(2), Command(allow: true), CancellationToken.None));
            remote.Command = () => Task.FromResult(RemoteResult<FriendsResponse>.Success(Response(null, T.Operation, time: Start - 1000000, allow: false)));
            await resumed.ExecuteAsync(T.Owner(2), pending!, CancellationToken.None);
            Assert.Null(await resumed.ReadPendingAsync(T.Owner(2), CancellationToken.None));
            Assert.Null((await resumed.ReadCachedAsync(T.Owner(2), new FriendsQuery("mine"), CancellationToken.None)).Value);
        }
        [Fact]
        public async Task AdmissionRetainsIdentityButAttestedCooldownAndHistoricalRejectionReleaseIt()
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            using var service = Service(db, remote);
            await service.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            remote.Command = () => Task.FromException<RemoteResult<FriendsResponse>>(new FriendsOperationException(new RemoteFailure(RemoteFailureKind.RateLimited), "friends.admission_limited", retryAfterMilliseconds: 60000));
            await Assert.ThrowsAsync<FriendsOperationException>(() => service.ExecuteAsync(T.Owner(), Command(), CancellationToken.None));
            Assert.NotNull(await service.ReadPendingAsync(T.Owner(), CancellationToken.None));
            await db.Current.ExecuteAsync(db.Scope, tx => { FriendsService.InvalidateCache(tx, db.Store, T.Owner(), T.Player, 99); return true; }, CancellationToken.None);
            remote.Command = () => Task.FromException<RemoteResult<FriendsResponse>>(new FriendsOperationException(new RemoteFailure(RemoteFailureKind.RateLimited), "friends.pair_cooldown", T.Operation, 86400000));
            await Assert.ThrowsAsync<FriendsOperationException>(() => service.ExecuteAsync(T.Owner(), Command(), CancellationToken.None));
            Assert.Null(await service.ReadPendingAsync(T.Owner(), CancellationToken.None));
        }

        [Fact]
        public async Task LocalReceiptCommitFailureRollsBackPendingDeletionForSameBodyRetry()
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            var state = new FaultStore(db.Store);
            using var service = new FriendsService(T.Owner(), remote, new FriendsStateCodec(), state, db.Current, () => Start);
            await service.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            state.Fail = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(T.Owner(), Command(), CancellationToken.None));
            Assert.NotNull(await service.ReadPendingAsync(T.Owner(), CancellationToken.None));
            Assert.NotNull((await service.ReadCachedAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None)).Value);
            state.Fail = false;
            await service.ExecuteAsync(T.Owner(), Command(), CancellationToken.None);
            Assert.Null(await service.ReadPendingAsync(T.Owner(), CancellationToken.None));
        }

        [Fact]
        public async Task CancellationAfterAdmissionPreservesIdentityAndDoesNotConfirmLateReply()
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            using var service = Service(db, remote);
            await service.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            var reached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reply = new TaskCompletionSource<RemoteResult<FriendsResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            remote.Command = () => { reached.SetResult(true); return reply.Task; };
            using var cancellation = new CancellationTokenSource();
            var command = service.ExecuteAsync(T.Owner(), Command(), cancellation.Token);
            await reached.Task;
            cancellation.Cancel();
            reply.SetResult(RemoteResult<FriendsResponse>.Success(Response(null, T.Operation, allow: false)));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command);
            Assert.Equal(T.Operation, (await service.ReadPendingAsync(T.Owner(), CancellationToken.None))!.OperationId.Value);
        }
        [Fact]
        public async Task RetiredOwnerNeverWritesLateReplyAndAuthDenialPurgesCache()
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            var service = Service(db, remote);
            await service.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            remote.Read = q => Task.FromResult(RemoteResult<FriendsResponse>.Failed(new RemoteFailure(RemoteFailureKind.Authorization)));
            await Assert.ThrowsAsync<FriendsOperationException>(() => service.ReadAsync(T.Owner(), new FriendsQuery("friends"), CancellationToken.None));
            Assert.Null((await service.ReadCachedAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None)).Value);
            var reached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reply = new TaskCompletionSource<RemoteResult<FriendsResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            remote.Read = q => { reached.SetResult(true); return reply.Task; };
            var read = service.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            await reached.Task;
            service.Dispose();
            reply.SetResult(RemoteResult<FriendsResponse>.Success(Response()));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => read);
        }
        [Fact]
        public async Task UnboundAuthenticationLossFencesConcurrentFirstRankBinding()
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            var rankRemote = new RankRemote();
            using var friends = Service(db, remote);
            using var ranks = new LeaderboardsService(T.Owner(), rankRemote, new LeaderboardsStateCodec(), db.Store, db.Current, () => Start);
            var reached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reply = new TaskCompletionSource<RemoteResult<LeaderboardsResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            rankRemote.Read = () => { reached.SetResult(true); return reply.Task; };
            var pending = ranks.QueryAsync(T.Owner(), new LeaderboardsQuery("players-monthly", "friends"), CancellationToken.None);
            await reached.Task;
            remote.Read = q => Task.FromResult(RemoteResult<FriendsResponse>.Failed(new RemoteFailure(RemoteFailureKind.Authorization)));
            await Assert.ThrowsAsync<FriendsOperationException>(() => friends.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None));
            reply.SetResult(RemoteResult<LeaderboardsResponse>.Success(Ranking()));
            await Assert.ThrowsAsync<LeaderboardsOperationException>(() => pending);
            Assert.Equal(Guid.Empty, friends.PlayerId);
            Assert.Null((await ranks.ReadCachedAsync(T.Owner(), new LeaderboardsQuery("players-monthly", "friends"), CancellationToken.None)).Value);
        }
        [Theory]
        [InlineData("friends.future_unknown", RemoteFailureKind.Validation)]
        [InlineData("friends.admission_limited", RemoteFailureKind.RateLimited)]
        [InlineData("friends.target_unavailable", RemoteFailureKind.Authorization)]
        public async Task UnapprovedOrAuthenticationRejectionCannotReleasePending(string code, RemoteFailureKind kind)
        {
            await using var db = await T.Database.Open();
            var remote = new Remote();
            using var friends = Service(db, remote);
            await friends.QueryAsync(T.Owner(), new FriendsQuery("mine"), CancellationToken.None);
            remote.Command = () => Task.FromException<RemoteResult<FriendsResponse>>(new FriendsOperationException(new RemoteFailure(kind), code, T.Operation));
            await Assert.ThrowsAsync<FriendsOperationException>(() => friends.ExecuteAsync(T.Owner(), Command(), CancellationToken.None));
            Assert.NotNull(await friends.ReadPendingAsync(T.Owner(), CancellationToken.None));
        }
        private sealed class Remote : IFriendsRemote
        {
            public int Queries;
            public Func<FriendsQuery, Task<RemoteResult<FriendsResponse>>>? Read;
            public Func<Task<RemoteResult<FriendsResponse>>>? Command;
            public Task<RemoteResult<FriendsResponse>> QueryAsync(FriendsQuery query, CancellationToken token)
            {
                Queries++;
                return Read?.Invoke(query) ?? Task.FromResult(RemoteResult<FriendsResponse>.Success(Response(query.View)));
            }
            public Task<RemoteResult<FriendsResponse>> ExecuteAsync(FriendsCommand command, CancellationToken token) => Command?.Invoke() ?? Task.FromResult(RemoteResult<FriendsResponse>.Success(Response(null, command.OperationId.Value, allow: command.Payload.AllowRequests!.Value)));
        }
        private sealed class RankRemote : ILeaderboardsRemote
        {
            public int Queries;
            public Func<Task<RemoteResult<LeaderboardsResponse>>>? Read;
            public Task<RemoteResult<LeaderboardsResponse>> QueryAsync(LeaderboardsQuery query, CancellationToken token)
            {
                Queries++;
                return Read?.Invoke() ?? Task.FromResult(RemoteResult<LeaderboardsResponse>.Success(Ranking()));
            }
            public Task<RemoteResult<LeaderboardsCountryResponse>> SetCountryAsync(LeaderboardsCountryCommand command, CancellationToken token) => throw new NotSupportedException();
        }
        private sealed class FaultStore : IDurableFeatureStateStore
        {
            private readonly IDurableFeatureStateStore inner;
            public bool Fail;
            public FaultStore(IDurableFeatureStateStore inner) => this.inner = inner;
            public Task<DurableFeatureState?> ReadAsync(ScopedOwnerContext owner, string ns, string key, CancellationToken token) => inner.ReadAsync(owner, ns, key, token);
            public DurableFeatureState? Read(ILocalStorageTransaction tx, ScopedOwnerContext owner, string ns, string key) => inner.Read(tx, owner, ns, key);
            public void Upsert(ILocalStorageTransaction tx, DurableFeatureMutation mutation)
            {
                if (Fail && mutation.EntityKey == "epoch")
                    throw new InvalidOperationException("Injected receipt commit fault.");
                inner.Upsert(tx, mutation);
            }
            public void Delete(ILocalStorageTransaction tx, ScopedOwnerContext owner, string ns, string key) => inner.Delete(tx, owner, ns, key);
        }
    }
}
