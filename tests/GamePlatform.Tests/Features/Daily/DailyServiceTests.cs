using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Daily;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Cache;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;
using GamePlatform.Transport.Http;
namespace GamePlatform.Tests.Features.Daily
{
    public sealed class DailyServiceTests
    {
        private const long Oct15 = 1_792_060_800_000;
        private const long Oct16 = 1_792_147_200_000;
        internal static readonly Guid App = Guid.Parse("0199f9a0-2000-7000-8000-000000000001");
        internal static readonly Guid Account = Guid.Parse("0199f9a0-2000-7000-8000-000000000002");
        internal static readonly Guid OtherAccount = Guid.Parse("0199f9a0-2000-7000-8000-000000000003");
        internal static readonly Guid ClaimId = Guid.Parse("0199f9a0-2000-7000-8000-000000000004");
        private static readonly ClientStreamId Stream = new ClientStreamId(Guid.Parse("0199f9a0-2000-7000-8000-000000000005"));
        private static readonly Guid Installation = Guid.Parse("0199f9a0-2000-7000-8000-000000000006");
        private static readonly DailyFeatureCodec Codec = new DailyFeatureCodec();
        private static readonly UuidV7Generator Ids = new UuidV7Generator(new FixedClock(), new CryptographicUuidRandomSource());
        internal static ScopedOwnerContext Owner(long generation = 1, Guid? account = null) =>
            new ScopedOwnerContext(new OwnerScope(new BackendNamespace("daily-test"), new AppId(App), new PlatformUserId(account ?? Account)), new SemanticId("private"), generation);
        internal static DailyStatus Status(string utcDate = "2026-10-15", bool eligible = true, int cycleDay = 3, int policyVersion = 2, DailyClaimEntry[]? history = null) =>
            new DailyStatus(utcDate, Oct16, Oct15, eligible, cycleDay, policyVersion, new[] { new DailyPlannedLine("currency", "coins", 9_007_199_254_740_993) }, history ?? Array.Empty<DailyClaimEntry>());
        internal static DailyClaimEntry Entry(string utcDate = "2026-10-15", int cycleDay = 3, Guid? claimId = null) =>
            new DailyClaimEntry(utcDate, cycleDay, claimId ?? ClaimId, 2, new[] { new DailyReceiptLine(0, 1, "currency", "coins", 9_007_199_254_740_993) });
        private static DailyService Service(Database db, IDailyRemote? remote, long generation = 1, long now = Oct15, ScopedOwnerContext? owner = null) =>
            new DailyService(owner ?? Owner(generation), remote, Codec, db.Store, db.Current, () => now, Stream, db.Commands(), Codec, Ids);

        [Fact]
        public async Task OfflineReadReturnsLastConfirmedStatusAsStaleWithoutFabricatingEligibility()
        {
            await using var db = await Database.Open();
            using (var online = Service(db, new Remote { Status = Status(eligible: false, history: new[] { Entry() }) }))
                Assert.Equal(SnapshotFreshness.Current, (await online.ReadAsync(Owner(), CancellationToken.None)).Freshness);
            await db.Reopen();
            using var offline = Service(db, null, 2, Oct16);
            var cached = await offline.ReadAsync(Owner(2), CancellationToken.None);
            Assert.Equal(SnapshotFreshness.Stale, cached.Freshness);
            Assert.Equal(FeatureSnapshotState.Stale, cached.State);
            Assert.Equal(Oct15, cached.LastConfirmedAtMilliseconds);
            Assert.False(cached.Value!.Eligible);
            Assert.Equal("2026-10-15", cached.Value.UtcDate);
            Assert.Equal(ClaimId, cached.Value.History[0].ClaimId);
            Assert.Equal(9_007_199_254_740_993, cached.Value.Reward[0].Quantity);
        }

        [Fact]
        public async Task RemoteReadReplacesTheCache()
        {
            await using var db = await Database.Open();
            var remote = new Remote { Status = Status(cycleDay: 3) };
            using var service = Service(db, remote);
            await service.ReadAsync(Owner(), CancellationToken.None);
            remote.Status = Status(utcDate: "2026-10-16", cycleDay: 4);
            var current = await service.ReadAsync(Owner(), CancellationToken.None);
            Assert.Equal(SnapshotFreshness.Current, current.Freshness);
            var cached = await service.ReadCachedAsync(Owner(), CancellationToken.None);
            Assert.Equal(("2026-10-16", 4), (cached.Value!.UtcDate, cached.Value.CycleDay));
            Assert.Equal(2, remote.Reads);
        }

        [Fact]
        public async Task ClaimAdmissionIsImmutableAndIdenticalRetryReturnsTheSameAdmission()
        {
            await using var db = await Database.Open();
            using var service = Service(db, null);
            var first = await service.ClaimAsync(Owner(), Status(), CancellationToken.None);
            var second = await service.ClaimAsync(Owner(), Status(), CancellationToken.None);
            Assert.Equal(first.OperationId, second.OperationId);
            Assert.Equal(first.Sequence, second.Sequence);
            Assert.Equal(1, await db.Scalar<long>("SELECT COUNT(*) FROM gp_outbox"));
            Assert.Equal("daily.claim:2:2026-10-15", await db.Scalar<string>("SELECT business_run_id FROM gp_outbox"));
            Assert.Equal("daily.claim", await db.Scalar<string>("SELECT operation_kind FROM gp_outbox"));
        }

        [Fact]
        public async Task ClaimIsRejectedLocallyWhenNotEligible()
        {
            await using var db = await Database.Open();
            using var service = Service(db, null);
            var error = await Assert.ThrowsAsync<DailyOperationException>(() => service.ClaimAsync(Owner(), Status(eligible: false), CancellationToken.None));
            Assert.Equal("daily_not_eligible", error.ResultCode);
            Assert.Equal(0, await db.Scalar<long>("SELECT COUNT(*) FROM gp_outbox"));
        }

        [Fact]
        public async Task ClaimUsesTheServerDateEvenWhenTheDeviceClockIsOnAnotherDay()
        {
            await using var db = await Database.Open();
            using var service = Service(db, null, now: Oct16);
            await service.ClaimAsync(Owner(), Status(utcDate: "2026-10-15"), CancellationToken.None);
            var body = await db.Scalar<byte[]>("SELECT semantic_body FROM gp_outbox");
            Assert.Equal("2026-10-15", new DailyMessagePackCodec().DecodeClaimCommand(body).UtcDate);
            Assert.Equal("daily.claim:2:2026-10-15", await db.Scalar<string>("SELECT business_run_id FROM gp_outbox"));
        }

        [Fact]
        public async Task AnotherAccountCannotReadOrWriteThisAccountsCache()
        {
            await using var db = await Database.Open();
            using var mine = Service(db, new Remote { Status = Status() });
            await mine.ReadAsync(Owner(), CancellationToken.None);
            using var other = Service(db, new Remote { Status = Status(cycleDay: 7) }, owner: Owner(1, OtherAccount));
            await Assert.ThrowsAnyAsync<Exception>(() => other.ReadCachedAsync(Owner(1, OtherAccount), CancellationToken.None));
            await Assert.ThrowsAnyAsync<Exception>(() => other.ReadAsync(Owner(1, OtherAccount), CancellationToken.None));
            await Assert.ThrowsAnyAsync<Exception>(() => other.ClaimAsync(Owner(1, OtherAccount), Status(), CancellationToken.None));
            Assert.Equal(3, (await mine.ReadCachedAsync(Owner(), CancellationToken.None)).Value!.CycleDay);
            Assert.Equal(0, await db.Scalar<long>("SELECT COUNT(*) FROM gp_outbox"));
        }

        [Fact]
        public async Task DisposalFencesALateRemoteResultWithoutCaching()
        {
            await using var db = await Database.Open();
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reply = new TaskCompletionSource<RemoteResult<DailyStatus>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var remote = new Remote { Read = () => { started.SetResult(true); return reply.Task; } };
            var service = Service(db, remote);
            var task = service.ReadAsync(Owner(), CancellationToken.None);
            await started.Task;
            service.Dispose();
            reply.SetResult(RemoteResult<DailyStatus>.Success(Status()));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => task);
            using var next = Service(db, null, 2);
            Assert.Null((await next.ReadCachedAsync(Owner(2), CancellationToken.None)).Value);
        }

        [Fact]
        public async Task ConfirmedReceiptAppendsHistoryAndEndsEligibilityForThatDate()
        {
            await using var db = await Database.Open();
            using var service = Service(db, new Remote { Status = Status(history: new[] { Entry("2026-10-14", 2, Guid.Parse("0199f9a0-2000-7000-8000-000000000007")) }) });
            await service.ReadAsync(Owner(), CancellationToken.None);
            var recorded = await service.RecordConfirmedAsync(Owner(), Entry(), CancellationToken.None);
            Assert.Equal(SnapshotFreshness.Stale, recorded.Freshness);
            Assert.False(recorded.Value!.Eligible);
            Assert.Equal(3, recorded.Value.CycleDay);
            Assert.Equal(2, recorded.Value.History.Count);
            Assert.Equal(("2026-10-15", ClaimId), (recorded.Value.History[0].UtcDate, recorded.Value.History[0].ClaimId));
            var again = await service.RecordConfirmedAsync(Owner(), Entry(), CancellationToken.None);
            Assert.Equal(2, again.Value!.History.Count);
            Assert.Equal(3, await db.Scalar<long>("SELECT revision FROM gp_feature_state WHERE entity_key = 'status'"));
        }

        [Fact]
        public async Task ConfirmedReceiptForAnEarlierDateKeepsTodaysEligibilityAndPrependsHistory()
        {
            await using var db = await Database.Open();
            using var service = Service(db, new Remote { Status = Status(utcDate: "2026-10-16", cycleDay: 4) });
            await service.ReadAsync(Owner(), CancellationToken.None);
            var recorded = await service.RecordConfirmedAsync(Owner(), Entry("2026-10-15", 3), CancellationToken.None);
            Assert.True(recorded.Value!.Eligible);
            Assert.Equal(("2026-10-16", 4), (recorded.Value.UtcDate, recorded.Value.CycleDay));
            Assert.Equal(("2026-10-15", 3, ClaimId), (recorded.Value.History[0].UtcDate, recorded.Value.History[0].CycleDay, recorded.Value.History[0].ClaimId));
        }

        [Fact]
        public async Task ConfirmedReceiptWithoutACachedStatusIsMissing()
        {
            await using var db = await Database.Open();
            using var service = Service(db, null);
            Assert.Equal(FeatureSnapshotState.Missing, (await service.RecordConfirmedAsync(Owner(), Entry(), CancellationToken.None)).State);
        }

        [Theory]
        [InlineData(RemoteFailureKind.Unavailable, "daily_unavailable")]
        [InlineData(RemoteFailureKind.Dependency, "daily_dependency")]
        public async Task UnavailableRemoteIsAnUnavailableSnapshotOrTheStaleCache(RemoteFailureKind kind, string code)
        {
            await using var db = await Database.Open();
            var remote = new Remote { Read = () => Task.FromResult(RemoteResult<DailyStatus>.Failed(new RemoteFailure(kind, 404))) };
            using var service = Service(db, remote);
            var unavailable = await service.ReadAsync(Owner(), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Unavailable, unavailable.State);
            Assert.Equal(code, unavailable.DiagnosticCode);
            Assert.Null(unavailable.Value);
            remote.Read = null;
            remote.Status = Status();
            await service.ReadAsync(Owner(), CancellationToken.None);
            remote.Read = () => throw new DailyOperationException(new RemoteFailure(kind, 404), code);
            var stale = await service.ReadAsync(Owner(), CancellationToken.None);
            Assert.Equal(FeatureSnapshotState.Stale, stale.State);
            Assert.True(stale.Value!.Eligible);
        }

        [Fact]
        public async Task AuthorizationDenialClearsTheCache()
        {
            await using var db = await Database.Open();
            var remote = new Remote { Status = Status() };
            using var service = Service(db, remote);
            await service.ReadAsync(Owner(), CancellationToken.None);
            remote.Read = () => Task.FromResult(RemoteResult<DailyStatus>.Failed(new RemoteFailure(RemoteFailureKind.Authorization, 403)));
            await Assert.ThrowsAsync<DailyOperationException>(() => service.ReadAsync(Owner(), CancellationToken.None));
            Assert.Null((await service.ReadCachedAsync(Owner(), CancellationToken.None)).Value);
        }

        internal sealed class Remote : IDailyRemote
        {
            public int Reads;
            public DailyStatus Status = DailyServiceTests.Status();
            public Func<Task<RemoteResult<DailyStatus>>>? Read;
            public Task<RemoteResult<DailyStatus>> StatusAsync(CancellationToken token)
            {
                Reads++;
                return Read?.Invoke() ?? Task.FromResult(RemoteResult<DailyStatus>.Success(Status));
            }
        }
        private sealed class FixedClock : IUnixMillisecondClock
        {
            public long GetUnixMilliseconds() => Oct15;
        }
        private sealed class TestFingerprint : ICommandFingerprint
        {
            public int GetFingerprintLength(int version) => version == 1 ? 32 : 0;
            public void Compute(OwnerScope owner, OperationId operation, ClientStreamId stream, Guid installation, long sequence, string kind, int schema, int fingerprint, long clientCreatedAt, ReadOnlySpan<byte> body, Span<byte> destination) => SHA256.HashData(body).CopyTo(destination);
        }
        internal sealed class Database : IAsyncDisposable
        {
            private readonly string path = Path.Combine(Path.GetTempPath(), "daily-sdk-" + Guid.NewGuid().ToString("N") + ".sqlite");
            public readonly StorageScope Scope = new StorageScope("daily-test", new PlatformId(App.ToString()), new PlatformId(Account.ToString()));
            public SqliteDatabase Current = null!;
            public SqliteDurableFeatureStateStore Store => new SqliteDurableFeatureStateStore(Current, Scope);
            public SqliteAtomicCommandStore Commands() => new SqliteAtomicCommandStore(Current, Scope, Owner().Owner, new TestFingerprint(), new FixedClock());
            public static async Task<Database> Open()
            {
                var value = new Database();
                await value.Reopen();
                await value.Current.ExecuteAsync(value.Scope, transaction =>
                {
                    ((SqliteTransactionSession)transaction).Execute("INSERT INTO gp_stream_state(singleton, backend_namespace, app_id, account_id, client_stream_id, installation_id, ready, next_sequence, local_revision, finalized_through) VALUES (1, ?, ?, ?, ?, ?, 1, 1, 0, 0)",
                        "daily-test", App.ToString(), Account.ToString(), Stream.ToString(), Installation.ToString("D"));
                    return true;
                }, CancellationToken.None);
                return value;
            }
            public async Task Reopen()
            {
                if (Current != null)
                    Assert.True(await Current.DisposeAsync(TimeSpan.FromSeconds(5)));
                Current = await SqliteDatabase.OpenAsync(path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            }
            public Task<T> Scalar<T>(string sql) => Current.ExecuteAsync(Scope, transaction => ((SqliteTransactionSession)transaction).ExecuteScalar<T>(sql), CancellationToken.None);
            public async ValueTask DisposeAsync()
            {
                Assert.True(await Current.DisposeAsync(TimeSpan.FromSeconds(5)));
                foreach (string suffix in new[] { "", "-wal", "-shm" })
                    if (File.Exists(path + suffix))
                        File.Delete(path + suffix);
            }
        }
    }
}
