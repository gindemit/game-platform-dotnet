using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Leaderboards;
using GamePlatform.Tests.Features.Teams;
using GamePlatform.Transport.Http;
namespace GamePlatform.Tests.Features.Leaderboards
{
    public sealed class LeaderboardsServiceTests
    {
        internal static LeaderboardsResponse Response(string board = "players-monthly", string scope = "global", string? country = null,
            long score = 9_007_199_254_740_993, LeaderboardEntry? self = null) => new LeaderboardsResponse(board, board.StartsWith("teams-") ? "team" : "player", scope, country,
            "2026-10", 1_790_812_800_000, 1_793_491_200_000, 1_793_491_200_000, TeamsServiceTests.Operation, 1_791_712_800_000, 1_791_713_100_000,
            "client_trusted_unvalidated", new[] { new LeaderboardEntry(TeamsServiceTests.Player, 1, score, "Fern 🌼", countryCode: country) }, null, self, 500,
            1_791_712_800_000, new LeaderboardsCountrySelection(country, null, null));
        internal static LeaderboardsCountryCommand Command(string country = "DE", string board = "players-monthly", Guid? operation = null) =>
            new LeaderboardsCountryCommand(new OperationId(operation ?? TeamsServiceTests.Operation), board, "player", country);
        internal static LeaderboardsCountryResponse CountryResponse(LeaderboardsCountryCommand? command = null)
        {
            var x = command ?? Command();
            return new LeaderboardsCountryResponse(x.OperationId.Value, x.BoardId, x.ParticipantKind, x.TeamId ?? TeamsServiceTests.Player, x.CountryCode, 1_793_491_200_000);
        }
        private static LeaderboardsService Service(TeamsServiceTests.Database db, ILeaderboardsRemote? remote, long generation = 1) =>
            new LeaderboardsService(TeamsServiceTests.Owner(generation), remote, new LeaderboardsStateCodec(), db.Store, db.Current, () => 1_791_712_800_000);
        [Fact]
        public async Task AmbiguousCountrySurvivesRealSqliteReopenAndRejectsChangedRetry()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            var remote = new Remote { CountryResult = RemoteResult<LeaderboardsCountryResponse>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)) };
            using (var service = Service(db, remote))
                await Assert.ThrowsAsync<LeaderboardsOperationException>(() => service.SetCountryAsync(TeamsServiceTests.Owner(), Command(), CancellationToken.None));
            await db.Reopen();
            using var reopened = Service(db, remote, 2);
            var pending = await reopened.ReadPendingAsync(TeamsServiceTests.Owner(2), CancellationToken.None);
            Assert.Equal(new LeaderboardsStateCodec().EncodeCountry(Command()), new LeaderboardsStateCodec().EncodeCountry(pending!));
            await Assert.ThrowsAsync<LeaderboardsOperationException>(() => reopened.SetCountryAsync(TeamsServiceTests.Owner(2), Command("FR"), CancellationToken.None));
            Assert.Equal(1, remote.Commands);
            remote.CountryResult = RemoteResult<LeaderboardsCountryResponse>.Success(CountryResponse());
            await reopened.SetCountryAsync(TeamsServiceTests.Owner(2), pending!, CancellationToken.None);
            Assert.Null(await reopened.ReadPendingAsync(TeamsServiceTests.Owner(2), CancellationToken.None));
            Assert.Equal(2, remote.Commands);
        }
        [Fact]
        public async Task CacheIsStaleScopedBoundedAndNeverUsedForContinuation()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            var remote = new Remote();
            using (var service = Service(db, remote))
            {
                await service.QueryAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("players-monthly"), CancellationToken.None);
                remote.Read = () => Task.FromResult(RemoteResult<LeaderboardsResponse>.Success(Response(scope: "country", country: "DE")));
                await service.QueryAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("players-monthly", "country", "DE"), CancellationToken.None);
            }
            await db.Reopen();
            using var offline = Service(db, null, 2);
            var cached = await offline.ReadCachedAsync(TeamsServiceTests.Owner(2), new LeaderboardsQuery("players-monthly"), CancellationToken.None);
            Assert.Equal(SnapshotFreshness.Stale, cached.Freshness);
            Assert.Equal(0, cached.Revision);
            Assert.Null(cached.Value!.Self);
            Assert.Equal(9_007_199_254_740_993, cached.Value.Entries[0].Score);
            Assert.Null((await offline.ReadCachedAsync(TeamsServiceTests.Owner(2), new LeaderboardsQuery("players-monthly", "country", "FR"), CancellationToken.None)).Value);
            Assert.Null((await offline.ReadCachedAsync(TeamsServiceTests.Owner(2), new LeaderboardsQuery("players-monthly", cursor: "abcdefghijklmnop"), CancellationToken.None)).Value);
            await Assert.ThrowsAsync<LeaderboardsOperationException>(() => offline.QueryAsync(TeamsServiceTests.Owner(2), new LeaderboardsQuery("players-monthly"), CancellationToken.None));
        }
        [Fact]
        public async Task DisposeAndChangedOwnerFenceLateReadWithoutCache()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reply = new TaskCompletionSource<RemoteResult<LeaderboardsResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var remote = new Remote { Read = () => { started.SetResult(true); return reply.Task; } };
            var service = Service(db, remote);
            await Assert.ThrowsAsync<LeaderboardsOperationException>(() => service.QueryAsync(TeamsServiceTests.Owner(2), new LeaderboardsQuery("players-monthly"), CancellationToken.None));
            var task = service.QueryAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("players-monthly"), CancellationToken.None);
            await started.Task;
            service.Dispose();
            reply.SetResult(RemoteResult<LeaderboardsResponse>.Success(Response()));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => task);
            using var next = Service(db, null, 2);
            Assert.Null((await next.ReadCachedAsync(TeamsServiceTests.Owner(2), new LeaderboardsQuery("players-monthly"), CancellationToken.None)).Value);
        }
        [Fact]
        public async Task ForeignCountryReceiptAndCoarseConflictRetainRetry()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            var remote = new Remote();
            using var service = Service(db, remote);
            remote.CountryResult = RemoteResult<LeaderboardsCountryResponse>.Success(CountryResponse(Command(operation: TeamsServiceTests.Team)));
            await Assert.ThrowsAsync<LeaderboardsOperationException>(() => service.SetCountryAsync(TeamsServiceTests.Owner(), Command(), CancellationToken.None));
            Assert.NotNull(await service.ReadPendingAsync(TeamsServiceTests.Owner(), CancellationToken.None));
            remote.CountryResult = RemoteResult<LeaderboardsCountryResponse>.Failed(new RemoteFailure(RemoteFailureKind.Conflict));
            await Assert.ThrowsAsync<LeaderboardsOperationException>(() => service.SetCountryAsync(TeamsServiceTests.Owner(), Command(), CancellationToken.None));
            Assert.NotNull(await service.ReadPendingAsync(TeamsServiceTests.Owner(), CancellationToken.None));
        }
        [Theory]
        [InlineData(RemoteFailureKind.Authorization)]
        [InlineData(RemoteFailureKind.RateLimited)]
        [InlineData(RemoteFailureKind.Authentication)]
        public async Task LostAcknowledgementThenGatewayFailureSurvivesReopenWithSameIdentity(RemoteFailureKind retryFailure)
        {
            await using var db = await TeamsServiceTests.Database.Open();
            var remote = new Remote { CountryResult = RemoteResult<LeaderboardsCountryResponse>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)) };
            using (var service = Service(db, remote))
            {
                await Assert.ThrowsAsync<LeaderboardsOperationException>(() => service.SetCountryAsync(TeamsServiceTests.Owner(), Command(), CancellationToken.None));
                remote.CountryResult = RemoteResult<LeaderboardsCountryResponse>.Failed(new RemoteFailure(retryFailure));
                await Assert.ThrowsAsync<LeaderboardsOperationException>(() => service.SetCountryAsync(TeamsServiceTests.Owner(), Command(), CancellationToken.None));
            }
            await db.Reopen();
            using var reopened = Service(db, remote, 2);
            var retained = await reopened.ReadPendingAsync(TeamsServiceTests.Owner(2), CancellationToken.None);
            Assert.Equal(new LeaderboardsStateCodec().EncodeCountry(Command()), new LeaderboardsStateCodec().EncodeCountry(retained!));
            remote.CountryResult = RemoteResult<LeaderboardsCountryResponse>.Success(CountryResponse());
            await reopened.SetCountryAsync(TeamsServiceTests.Owner(2), retained!, CancellationToken.None);
            Assert.Null(await reopened.ReadPendingAsync(TeamsServiceTests.Owner(2), CancellationToken.None));
        }
        [Fact]
        public async Task WrongCohortCannotBecomeCurrentOrCached()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            using var service = Service(db, new Remote());
            await Assert.ThrowsAsync<LeaderboardsOperationException>(() => service.QueryAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("players-monthly", "country", "DE"), CancellationToken.None));
            Assert.Null((await service.ReadCachedAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("players-monthly", "country", "DE"), CancellationToken.None)).Value);
        }
        [Fact]
        public async Task CallerCancellationAfterIgnoredRemoteReplyCannotPublishOrWrite()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            using var cancellation = new CancellationTokenSource();
            var remote = new Remote { Read = () => { cancellation.Cancel(); return Task.FromResult(RemoteResult<LeaderboardsResponse>.Success(Response())); } };
            using var service = Service(db, remote);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.QueryAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("players-monthly"), cancellation.Token));
            Assert.Null((await service.ReadCachedAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("players-monthly"), CancellationToken.None)).Value);
        }
        [Fact]
        public async Task AuthorizationDenialClearsStaleAssignmentButKeepsUncertainOwnerReceipt()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            var remote = new Remote();
            using var service = Service(db, remote);
            await service.QueryAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("players-monthly"), CancellationToken.None);
            remote.CountryResult = RemoteResult<LeaderboardsCountryResponse>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain));
            await Assert.ThrowsAsync<LeaderboardsOperationException>(() => service.SetCountryAsync(TeamsServiceTests.Owner(), Command(), CancellationToken.None));
            remote.CountryError = new LeaderboardsOperationException(new RemoteFailure(RemoteFailureKind.Authorization), "leaderboards.owner_unavailable");
            await Assert.ThrowsAsync<LeaderboardsOperationException>(() => service.SetCountryAsync(TeamsServiceTests.Owner(), Command(), CancellationToken.None));
            Assert.NotNull(await service.ReadPendingAsync(TeamsServiceTests.Owner(), CancellationToken.None));
            Assert.Null((await service.ReadCachedAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("players-monthly"), CancellationToken.None)).Value);
        }
        [Fact]
        public async Task CountryAcknowledgementInvalidatesOnlyThatBoardAndNeverInventsProjectionRevision()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            var remote = new Remote();
            using var service = Service(db, remote);
            await service.QueryAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("players-monthly"), CancellationToken.None);
            remote.Read = () => Task.FromResult(RemoteResult<LeaderboardsResponse>.Success(Response("teams-monthly")));
            await service.QueryAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("teams-monthly"), CancellationToken.None);
            var ack = await service.SetCountryAsync(TeamsServiceTests.Owner(), Command(), CancellationToken.None);
            Assert.Equal(1_793_491_200_000, ack.EffectiveAt);
            Assert.Null((await service.ReadCachedAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("players-monthly"), CancellationToken.None)).Value);
            Assert.NotNull((await service.ReadCachedAsync(TeamsServiceTests.Owner(), new LeaderboardsQuery("teams-monthly"), CancellationToken.None)).Value);
        }
        internal sealed class Remote : ILeaderboardsRemote
        {
            public int Commands;
            public Func<Task<RemoteResult<LeaderboardsResponse>>>? Read;
            public LeaderboardsOperationException? CountryError;
            public RemoteResult<LeaderboardsCountryResponse> CountryResult = RemoteResult<LeaderboardsCountryResponse>.Success(CountryResponse());
            public Task<RemoteResult<LeaderboardsResponse>> QueryAsync(LeaderboardsQuery query, CancellationToken token) => Read?.Invoke() ?? Task.FromResult(RemoteResult<LeaderboardsResponse>.Success(Response()));
            public Task<RemoteResult<LeaderboardsCountryResponse>> SetCountryAsync(LeaderboardsCountryCommand command, CancellationToken token)
            {
                Commands++;
                return CountryError == null ? Task.FromResult(CountryResult) : Task.FromException<RemoteResult<LeaderboardsCountryResponse>>(CountryError);
            }
        }
    }
}
