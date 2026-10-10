#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Teams;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Transport.Http;

namespace GamePlatform.Tests.Features.Teams
{
    public sealed class TeamsServiceTests
    {
        internal static readonly Guid App = Guid.Parse("0199f9a0-1000-7000-8000-000000000001");
        internal static readonly Guid Account = Guid.Parse("0199f9a0-1000-7000-8000-000000000002");
        internal static readonly Guid Player = Guid.Parse("0199f9a0-1000-7000-8000-000000000003");
        internal static readonly Guid Team = Guid.Parse("0199f9a0-1000-7000-8000-000000000004");
        internal static readonly Guid Operation = Guid.Parse("0199f9a0-1000-7000-8000-000000000005");
        internal static ScopedOwnerContext Owner(long generation = 1) => new ScopedOwnerContext(new OwnerScope(new BackendNamespace("teams-test"), new AppId(App), new PlatformUserId(Account)), new SemanticId("private"), generation);
        internal static TeamsResponse Response(Guid? operation = null, long revision = 9, Guid? account = null) => new TeamsResponse(App, account ?? Account, Player, 1_790_000_000_000, operation, revision,
            new TeamSummary(Team, "Pocket Garden", "Growing together", "daisy", "en", "open", 0, 30, 1, revision, 1_790_000_000_000),
            new TeamMembership(Team, Player, "leader", 1_790_000_000_000, 9_007_199_254_740_993),
            Array.Empty<TeamSummary>(), Array.Empty<TeamMember>(), Array.Empty<TeamMessage>(), Array.Empty<TeamJoinRequest>(), Array.Empty<TeamInvitation>(), Array.Empty<TeamHelpRequest>(), null, operation.HasValue ? "teams_created" : null);
        internal static TeamsCommand Command(Guid? operation = null) => new TeamsCommand(new OperationId(operation ?? Operation), "send_message", Team, payload: new TeamsCommandPayload(text: "Hello gardeners 🌼"));

        [Fact]
        public async Task CacheReopensStaleAndNeverClaimsOfflineAuthority()
        {
            await using var db = await Database.Open();
            var codec = new TeamsStateCodec();
            using (var online = db.Service(new Remote(), codec, Owner()))
            {
                var fresh = await online.QueryAsync(Owner(), new TeamsQuery("mine"), CancellationToken.None);
                Assert.Equal(SnapshotFreshness.Current, fresh.Freshness);
            }
            await db.Reopen();
            using var offline = db.Service(null, codec, Owner(2));
            var cached = await offline.ReadCachedAsync(Owner(2), new TeamsQuery("mine"), CancellationToken.None);
            Assert.Equal(SnapshotFreshness.Stale, cached.Freshness);
            Assert.Equal(9_007_199_254_740_993, cached.Value!.Membership!.HelpCount);
            Assert.Equal("teams_offline", (await Assert.ThrowsAsync<TeamsOperationException>(() => offline.ExecuteAsync(Owner(2), Command(), CancellationToken.None))).ResultCode);
            Assert.Null(await offline.ReadPendingAsync(Owner(2), CancellationToken.None));
        }

        [Fact]
        public async Task AmbiguousCommandSurvivesReopenAndOnlyIdenticalExplicitRetryCanSend()
        {
            await using var db = await Database.Open();
            var codec = new TeamsStateCodec();
            var remote = new Remote { CommandResult = RemoteResult<TeamsResponse>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)) };
            using (var first = db.Service(remote, codec, Owner()))
            {
                var failed = await Assert.ThrowsAsync<TeamsOperationException>(() => first.ExecuteAsync(Owner(), Command(), CancellationToken.None));
                Assert.Equal(RemoteFailureKind.OutcomeUncertain, failed.Failure.Kind);
            }
            await db.Reopen();
            using var resumed = db.Service(remote, codec, Owner(2));
            var pending = await resumed.ReadPendingAsync(Owner(2), CancellationToken.None);
            Assert.Equal(codec.EncodeCommand(Command()), codec.EncodeCommand(pending!));
            var changed = new TeamsCommand(new OperationId(Operation), "send_message", Team, payload: new TeamsCommandPayload(text: "different"));
            Assert.Equal("teams_pending_command", (await Assert.ThrowsAsync<TeamsOperationException>(() => resumed.ExecuteAsync(Owner(2), changed, CancellationToken.None))).ResultCode);
            Assert.Equal(1, remote.Commands);
            remote.CommandResult = RemoteResult<TeamsResponse>.Success(Response(Operation));
            await resumed.ExecuteAsync(Owner(2), pending!, CancellationToken.None);
            Assert.Equal(2, remote.Commands);
            Assert.Null(await resumed.ReadPendingAsync(Owner(2), CancellationToken.None));
        }

        [Fact]
        public async Task DisposalFencesLateReadWithoutWritingCache()
        {
            await using var db = await Database.Open();
            var reached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completed = new TaskCompletionSource<RemoteResult<TeamsResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var remote = new Remote { Read = () => { reached.SetResult(true); return completed.Task; } };
            var service = db.Service(remote, new TeamsStateCodec(), Owner());
            var pending = service.QueryAsync(Owner(), new TeamsQuery("mine"), CancellationToken.None);
            await reached.Task;
            service.Dispose();
            completed.SetResult(RemoteResult<TeamsResponse>.Success(Response()));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
            using var next = db.Service(null, new TeamsStateCodec(), Owner(2));
            Assert.Equal(SnapshotFreshness.Missing, (await next.ReadCachedAsync(Owner(2), new TeamsQuery("mine"), CancellationToken.None)).Freshness);
        }

        [Fact]
        public async Task CapturedOwnerAndRemoteAccountAreBothFenced()
        {
            await using var db = await Database.Open();
            var remote = new Remote { Read = () => Task.FromResult(RemoteResult<TeamsResponse>.Success(Response(account: Team))) };
            using var service = db.Service(remote, new TeamsStateCodec(), Owner());
            await Assert.ThrowsAsync<TeamsOperationException>(() => service.QueryAsync(Owner(2), new TeamsQuery("mine"), CancellationToken.None));
            Assert.Equal(0, remote.Queries);
            await Assert.ThrowsAsync<TeamsOperationException>(() => service.QueryAsync(Owner(), new TeamsQuery("mine"), CancellationToken.None));
            Assert.Equal(SnapshotFreshness.Missing, (await service.ReadCachedAsync(Owner(), new TeamsQuery("mine"), CancellationToken.None)).Freshness);
        }

        [Fact]
        public async Task FeedInvalidationIsAtomicAndPreservesInterruptedCommand()
        {
            await using var db = await Database.Open();
            var remote = new Remote { CommandResult = RemoteResult<TeamsResponse>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)) };
            using var service = db.Service(remote, new TeamsStateCodec(), Owner());
            await service.QueryAsync(Owner(), new TeamsQuery("mine"), CancellationToken.None);
            await Assert.ThrowsAsync<TeamsOperationException>(() => service.ExecuteAsync(Owner(), Command(), CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.Current.ExecuteAsync<bool>(db.Scope, tx => { TeamsService.InvalidateCache(tx, db.Store, Owner()); throw new InvalidOperationException(); }, CancellationToken.None));
            Assert.NotNull((await service.ReadCachedAsync(Owner(), new TeamsQuery("mine"), CancellationToken.None)).Value);
            await db.Current.ExecuteAsync(db.Scope, tx => { TeamsService.InvalidateCache(tx, db.Store, Owner()); return true; }, CancellationToken.None);
            Assert.Null((await service.ReadCachedAsync(Owner(), new TeamsQuery("mine"), CancellationToken.None)).Value);
            Assert.NotNull(await service.ReadPendingAsync(Owner(), CancellationToken.None));
        }

        [Fact]
        public async Task TerminalRejectionClearsPendingButForeignReceiptDoesNot()
        {
            await using var db = await Database.Open();
            var remote = new Remote { CommandResult = RemoteResult<TeamsResponse>.Failed(new RemoteFailure(RemoteFailureKind.Conflict)) };
            using var service = db.Service(remote, new TeamsStateCodec(), Owner());
            await Assert.ThrowsAsync<TeamsOperationException>(() => service.ExecuteAsync(Owner(), Command(), CancellationToken.None));
            Assert.Null(await service.ReadPendingAsync(Owner(), CancellationToken.None));
            remote.CommandResult = RemoteResult<TeamsResponse>.Success(Response(Team));
            await Assert.ThrowsAsync<TeamsOperationException>(() => service.ExecuteAsync(Owner(), Command(), CancellationToken.None));
            Assert.NotNull(await service.ReadPendingAsync(Owner(), CancellationToken.None));
        }

        [Fact]
        public async Task LateQueryCannotRestorePrivateCacheAfterFeedInvalidation()
        {
            await using var db = await Database.Open();
            var reached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completed = new TaskCompletionSource<RemoteResult<TeamsResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var remote = new Remote { Read = () => { reached.SetResult(true); return completed.Task; } };
            using var service = db.Service(remote, new TeamsStateCodec(), Owner());
            var query = service.QueryAsync(Owner(), new TeamsQuery("messages", Team), CancellationToken.None);
            await reached.Task;
            await db.Current.ExecuteAsync(db.Scope, tx => { TeamsService.InvalidateCache(tx, db.Store, Owner()); return true; }, CancellationToken.None);
            completed.SetResult(RemoteResult<TeamsResponse>.Success(Response()));
            Assert.Equal("teams_refresh_required", (await Assert.ThrowsAsync<TeamsOperationException>(() => query)).ResultCode);
            Assert.Null((await service.ReadCachedAsync(Owner(), new TeamsQuery("messages", Team), CancellationToken.None)).Value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MembershipLossAndDefiniteDenialErasePrivateCachedViews(bool lostMembership)
        {
            await using var db = await Database.Open();
            var remote = new Remote();
            using var service = db.Service(remote, new TeamsStateCodec(), Owner());
            await service.QueryAsync(Owner(), new TeamsQuery("mine"), CancellationToken.None);
            await service.QueryAsync(Owner(), new TeamsQuery("messages", Team), CancellationToken.None);
            if (lostMembership)
            {
                var former = Response();
                var absent = new TeamsResponse(App, Account, Player, former.ServerTimeMilliseconds, null, 0, null, null,
                    former.Teams, former.Members, former.Messages, former.Requests, former.Invites, former.HelpRequests, null, null);
                remote.Read = () => Task.FromResult(RemoteResult<TeamsResponse>.Success(absent));
                await service.QueryAsync(Owner(), new TeamsQuery("mine"), CancellationToken.None);
            }
            else
            {
                remote.Read = () => Task.FromResult(RemoteResult<TeamsResponse>.Failed(new RemoteFailure(RemoteFailureKind.Authorization)));
                await Assert.ThrowsAsync<TeamsOperationException>(() => service.QueryAsync(Owner(), new TeamsQuery("messages", Team), CancellationToken.None));
            }
            Assert.Null((await service.ReadCachedAsync(Owner(), new TeamsQuery("messages", Team), CancellationToken.None)).Value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DefiniteCommandDenialClearsPrivateCacheAndPendingAcrossRestart(bool throws)
        {
            await using var db = await Database.Open();
            var remote = new Remote();
            using (var service = db.Service(remote, new TeamsStateCodec(), Owner()))
            {
                await service.QueryAsync(Owner(), new TeamsQuery("mine"), CancellationToken.None);
                await service.QueryAsync(Owner(), new TeamsQuery("messages", Team), CancellationToken.None);
                remote.CommandResult = RemoteResult<TeamsResponse>.Failed(new RemoteFailure(RemoteFailureKind.Authorization));
                if (throws) remote.CommandError = new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Authorization), "teams.membership_required");
                await Assert.ThrowsAsync<TeamsOperationException>(() => service.ExecuteAsync(Owner(), Command(), CancellationToken.None));
            }
            await db.Reopen();
            using var offline = db.Service(null, new TeamsStateCodec(), Owner(2));
            Assert.Null(await offline.ReadPendingAsync(Owner(2), CancellationToken.None));
            Assert.Equal(SnapshotFreshness.Missing, (await offline.ReadCachedAsync(Owner(2), new TeamsQuery("mine"), CancellationToken.None)).Freshness);
            Assert.Equal(SnapshotFreshness.Missing, (await offline.ReadCachedAsync(Owner(2), new TeamsQuery("messages", Team), CancellationToken.None)).Freshness);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RejectedHelpCooldownDoesNotBlockSubsequentChat(bool throws)
        {
            await using var db = await Database.Open();
            var remote = new Remote { CommandResult = RemoteResult<TeamsResponse>.Failed(new RemoteFailure(RemoteFailureKind.RateLimited, 429)) };
            if (throws) remote.CommandError = new TeamsOperationException(new RemoteFailure(RemoteFailureKind.RateLimited, 429), "teams.help_cooldown");
            using var service = db.Service(remote, new TeamsStateCodec(), Owner());
            var help = new TeamsCommand(new OperationId(Operation), "request_help", Team);
            await Assert.ThrowsAsync<TeamsOperationException>(() => service.ExecuteAsync(Owner(), help, CancellationToken.None));
            Assert.Null(await service.ReadPendingAsync(Owner(), CancellationToken.None));
            remote.CommandError = null;
            remote.CommandResult = RemoteResult<TeamsResponse>.Success(Response(Player));
            await service.ExecuteAsync(Owner(), Command(Player), CancellationToken.None);
            Assert.Equal(2, remote.Commands);
            Assert.Null(await service.ReadPendingAsync(Owner(), CancellationToken.None));
        }

        [Fact]
        public async Task TemporarilyDisabledOwnerErasesCacheButRetainsUncertainIdentity()
        {
            await using var db = await Database.Open();
            var remote = new Remote { CommandResult = RemoteResult<TeamsResponse>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)) };
            using (var service = db.Service(remote, new TeamsStateCodec(), Owner()))
            {
                await service.QueryAsync(Owner(), new TeamsQuery("messages", Team), CancellationToken.None);
                await Assert.ThrowsAsync<TeamsOperationException>(() => service.ExecuteAsync(Owner(), Command(), CancellationToken.None));
                remote.CommandError = new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Authorization, 403), "teams.owner_unavailable");
                await Assert.ThrowsAsync<TeamsOperationException>(() => service.ExecuteAsync(Owner(), Command(), CancellationToken.None));
            }
            await db.Reopen();
            using var offline = db.Service(null, new TeamsStateCodec(), Owner(2));
            Assert.Equal(Operation, (await offline.ReadPendingAsync(Owner(2), CancellationToken.None))!.OperationId.Value);
            Assert.Equal(SnapshotFreshness.Missing, (await offline.ReadCachedAsync(Owner(2), new TeamsQuery("messages", Team), CancellationToken.None)).Freshness);
        }

        internal sealed class Remote : ITeamsRemote
        {
            public int Queries, Commands;
            public Func<Task<RemoteResult<TeamsResponse>>>? Read;
            public TeamsOperationException? CommandError;
            public RemoteResult<TeamsResponse> CommandResult = RemoteResult<TeamsResponse>.Success(Response(Operation));
            public Task<RemoteResult<TeamsResponse>> QueryAsync(TeamsQuery query, CancellationToken token) { Queries++; return Read?.Invoke() ?? Task.FromResult(RemoteResult<TeamsResponse>.Success(Response())); }
            public Task<RemoteResult<TeamsResponse>> ExecuteAsync(TeamsCommand command, CancellationToken token) { Commands++; return CommandError == null ? Task.FromResult(CommandResult) : Task.FromException<RemoteResult<TeamsResponse>>(CommandError); }
        }
        internal sealed class Database : IAsyncDisposable
        {
            private readonly string path = Path.Combine(Path.GetTempPath(), "teams-sdk-" + Guid.NewGuid().ToString("N") + ".sqlite");
            public StorageScope Scope = new StorageScope("teams-test", new PlatformId(App.ToString()), new PlatformId(Account.ToString()));
            public SqliteDatabase Current = null!;
            public SqliteDurableFeatureStateStore Store => new SqliteDurableFeatureStateStore(Current, Scope);
            public static async Task<Database> Open() { var value = new Database(); await value.Reopen(); return value; }
            public async Task Reopen() { if (Current != null) Assert.True(await Current.DisposeAsync(TimeSpan.FromSeconds(5))); Current = await SqliteDatabase.OpenAsync(path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None); }
            public TeamsService Service(ITeamsRemote? remote, ITeamsStateCodec codec, ScopedOwnerContext owner) => new TeamsService(owner, remote, codec, Store, Current, () => 1_790_000_000_000);
            public async ValueTask DisposeAsync() { Assert.True(await Current.DisposeAsync(TimeSpan.FromSeconds(5))); foreach (string suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); }
        }
    }
}
