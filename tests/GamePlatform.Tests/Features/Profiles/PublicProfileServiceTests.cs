using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Profiles;
using GamePlatform.Tests.Features.Teams;
using GamePlatform.Transport.Http;

namespace GamePlatform.Tests.Features.Profiles
{
    public sealed class PublicProfileServiceTests
    {
        internal static readonly Guid Subject = Guid.Parse("0199f9a0-1000-7000-8000-000000000006");
        internal static readonly Guid OtherSubject = Guid.Parse("0199f9a0-1000-7000-8000-000000000007");

        internal static PublicProfileResponse Response(Guid? subject = null, bool available = true, Guid? app = null) =>
            new PublicProfileResponse(app ?? TeamsServiceTests.App, subject ?? Subject, 1_790_000_000_000, available
                ? new PublicProfile("Fern 🌼", "avatar.daisy", "en-GB", 9_007_199_254_740_993, new PublicProfileTeam(TeamsServiceTests.Team, "Pocket Garden", "daisy"))
                : null);

        private static PublicProfileService Service(TeamsServiceTests.Database db, IPublicProfileRemote? remote, ScopedOwnerContext? owner = null) =>
            new PublicProfileService(owner ?? TeamsServiceTests.Owner(), remote, new PublicProfileStateCodec(), db.Store, db.Current, () => 1_790_000_000_000);

        [Fact]
        public async Task ConfirmedReadIsCurrentThenSurvivesReopenAsStale()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            using (var service = Service(db, new Remote()))
            {
                var read = await service.QueryAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None);
                Assert.Equal(FeatureSnapshotState.Available, read.State);
                Assert.Equal(SnapshotFreshness.Current, read.Freshness);
                Assert.Equal("Fern 🌼", read.Value!.Profile!.DisplayName);
            }
            await db.Reopen();
            using var offline = Service(db, null, TeamsServiceTests.Owner(2));
            var cached = await offline.ReadCachedAsync(TeamsServiceTests.Owner(2), new PublicProfileQuery(Subject), CancellationToken.None);
            Assert.Equal(SnapshotFreshness.Stale, cached.Freshness);
            Assert.Equal(FeatureSnapshotState.Stale, cached.State);
            Assert.Equal(0, cached.Revision);
            Assert.Equal(9_007_199_254_740_993, cached.Value!.Profile!.Revision);
            Assert.Equal("en-GB", cached.Value.Profile.Locale);
            Assert.Equal(TeamsServiceTests.Team, cached.Value.Profile.Team!.TeamId);
            var never = await offline.ReadCachedAsync(TeamsServiceTests.Owner(2), new PublicProfileQuery(OtherSubject), CancellationToken.None);
            Assert.Equal(SnapshotFreshness.Missing, never.Freshness);
            Assert.Null(never.Value);
            var error = await Assert.ThrowsAsync<PublicProfileOperationException>(() => offline.QueryAsync(TeamsServiceTests.Owner(2), new PublicProfileQuery(Subject), CancellationToken.None));
            Assert.Equal(RemoteFailureKind.Unavailable, error.Failure.Kind);
        }

        [Fact]
        public async Task UnavailableSubjectIsStoredAsAvailableReadWithoutProfile()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            using var service = Service(db, new Remote { Reply = () => Task.FromResult(RemoteResult<PublicProfileResponse>.Success(Response(available: false))) });
            var read = await service.QueryAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Available, read.State);
            Assert.Null(read.Value!.Profile);
            var cached = await service.ReadCachedAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None);
            Assert.Equal(SnapshotFreshness.Stale, cached.Freshness);
            Assert.Equal(Subject, cached.Value!.PlayerId);
            Assert.Null(cached.Value.Profile);
        }

        [Theory]
        [InlineData(RemoteFailureKind.Dependency)]
        [InlineData(RemoteFailureKind.NotSent)]
        [InlineData(RemoteFailureKind.RateLimited)]
        public async Task RemoteFailureKeepsLastConfirmedReadAndCachesNothingNew(RemoteFailureKind failure)
        {
            await using var db = await TeamsServiceTests.Database.Open();
            var remote = new Remote();
            using var service = Service(db, remote);
            await service.QueryAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None);
            remote.Reply = () => Task.FromResult(RemoteResult<PublicProfileResponse>.Failed(new RemoteFailure(failure)));
            var error = await Assert.ThrowsAsync<PublicProfileOperationException>(() => service.QueryAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None));
            Assert.Equal(failure, error.Failure.Kind);
            Assert.Equal("Fern 🌼", (await service.ReadCachedAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None)).Value!.Profile!.DisplayName);
            await Assert.ThrowsAsync<PublicProfileOperationException>(() => service.QueryAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(OtherSubject), CancellationToken.None));
            Assert.Null((await service.ReadCachedAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(OtherSubject), CancellationToken.None)).Value);
        }

        [Fact]
        public async Task AuthorizationFailureForgetsCachedSubject()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            var remote = new Remote();
            using var service = Service(db, remote);
            await service.QueryAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None);
            remote.Reply = () => Task.FromResult(RemoteResult<PublicProfileResponse>.Failed(new RemoteFailure(RemoteFailureKind.Authorization)));
            await Assert.ThrowsAsync<PublicProfileOperationException>(() => service.QueryAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None));
            Assert.Null((await service.ReadCachedAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None)).Value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ForeignSubjectOrAppEchoIsRejectedAndNotCached(bool foreignSubject)
        {
            await using var db = await TeamsServiceTests.Database.Open();
            var reply = foreignSubject ? Response(OtherSubject) : Response(app: TeamsServiceTests.Operation);
            using var service = Service(db, new Remote { Reply = () => Task.FromResult(RemoteResult<PublicProfileResponse>.Success(reply)) });
            var error = await Assert.ThrowsAsync<PublicProfileOperationException>(() => service.QueryAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None));
            Assert.Equal(RemoteFailureKind.Protocol, error.Failure.Kind);
            Assert.Null((await service.ReadCachedAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None)).Value);
            Assert.Null((await service.ReadCachedAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(OtherSubject), CancellationToken.None)).Value);
        }

        [Fact]
        public async Task OtherViewAndOtherOwnerCannotSeeOrUseTheRead()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            using var service = Service(db, new Remote());
            await service.QueryAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None);
            var otherView = new ScopedOwnerContext(TeamsServiceTests.Owner().Owner, new SemanticId("other"), 1);
            using var viewService = Service(db, null, otherView);
            Assert.Null((await viewService.ReadCachedAsync(otherView, new PublicProfileQuery(Subject), CancellationToken.None)).Value);
            var otherAccount = new ScopedOwnerContext(new OwnerScope(new BackendNamespace("teams-test"), new AppId(TeamsServiceTests.App), new PlatformUserId(OtherSubject)), new SemanticId("private"), 1);
            foreach (var foreign in new[] { TeamsServiceTests.Owner(2), otherAccount })
            {
                var error = await Assert.ThrowsAsync<PublicProfileOperationException>(() => service.ReadCachedAsync(foreign, new PublicProfileQuery(Subject), CancellationToken.None));
                Assert.Equal(RemoteFailureKind.Authorization, error.Failure.Kind);
                await Assert.ThrowsAsync<PublicProfileOperationException>(() => service.QueryAsync(foreign, new PublicProfileQuery(Subject), CancellationToken.None));
            }
        }

        [Fact]
        public async Task LateResultAfterRetirementNeverPublishesOrWrites()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reply = new TaskCompletionSource<RemoteResult<PublicProfileResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var service = Service(db, new Remote { Reply = () => { started.SetResult(true); return reply.Task; } });
            var task = service.QueryAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None);
            await started.Task;
            service.Dispose();
            reply.SetResult(RemoteResult<PublicProfileResponse>.Success(Response()));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => task);
            using var next = Service(db, null, TeamsServiceTests.Owner(2));
            Assert.Null((await next.ReadCachedAsync(TeamsServiceTests.Owner(2), new PublicProfileQuery(Subject), CancellationToken.None)).Value);
        }

        [Fact]
        public async Task CallerCancellationAfterReplyCannotPublishOrWrite()
        {
            await using var db = await TeamsServiceTests.Database.Open();
            using var cancellation = new CancellationTokenSource();
            using var service = Service(db, new Remote { Reply = () => { cancellation.Cancel(); return Task.FromResult(RemoteResult<PublicProfileResponse>.Success(Response())); } });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.QueryAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), cancellation.Token));
            Assert.Null((await service.ReadCachedAsync(TeamsServiceTests.Owner(), new PublicProfileQuery(Subject), CancellationToken.None)).Value);
        }

        [Fact]
        public void ConstructorsRejectValuesOutsideThePublicAllowlistBounds()
        {
            Assert.Throws<ArgumentException>(() => new PublicProfileQuery(Guid.Empty));
            Assert.Throws<ArgumentException>(() => new PublicProfileTeam(Guid.Empty, "Pocket Garden", "daisy"));
            Assert.Throws<ArgumentException>(() => new PublicProfileResponse(Guid.Empty, Subject, 0, null));
            Assert.Throws<ArgumentException>(() => new PublicProfileResponse(TeamsServiceTests.App, Guid.Empty, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PublicProfileResponse(TeamsServiceTests.App, Subject, -1, null));
            Assert.Equal(string.Concat(System.Linq.Enumerable.Repeat("🌼", 64)), new PublicProfile(string.Concat(System.Linq.Enumerable.Repeat("🌼", 64)), null, null, 0, null).DisplayName);
            Assert.Equal("🌼🌼🌼", new PublicProfileTeam(TeamsServiceTests.Team, "🌼🌼🌼", "d").Name);
        }

        [Theory]
        [InlineData("", null, null, 0L)]
        [InlineData(" Fern", null, null, 0L)]
        [InlineData("Fern", "bad key", null, 0L)]
        [InlineData("Fern", null, "english", 0L)]
        [InlineData("Fern", null, null, -1L)]
        public void PublicProfileRejectsInvalidFields(string displayName, string? avatarKey, string? locale, long revision)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PublicProfile(displayName, avatarKey, locale, revision, null));
        }

        [Theory]
        [InlineData("ab", "daisy")]
        [InlineData("🌼🌼", "daisy")]
        [InlineData("Twenty-five characters!!!", "daisy")]
        [InlineData("Pocket Garden", "")]
        public void PublicProfileTeamRejectsInvalidFields(string name, string badgeId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PublicProfileTeam(TeamsServiceTests.Team, name, badgeId));
        }

        [Fact]
        public void LongAvatarAndBadgeAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PublicProfile("Fern", "a" + new string('b', 128), null, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PublicProfileTeam(TeamsServiceTests.Team, "Pocket Garden", new string('b', 49)));
        }

        internal sealed class Remote : IPublicProfileRemote
        {
            public Func<Task<RemoteResult<PublicProfileResponse>>>? Reply;
            public Task<RemoteResult<PublicProfileResponse>> QueryAsync(PublicProfileQuery query, CancellationToken token) =>
                Reply?.Invoke() ?? Task.FromResult(RemoteResult<PublicProfileResponse>.Success(Response(query.SubjectPlayerId)));
        }
    }
}
