#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Sync;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;
using GamePlatform.Storage.Sqlite.Sync;
using GamePlatform.Sync.Pull;
using GamePlatform.Sync.Push;

namespace GamePlatform.Tests.Sync.Pull
{
    public sealed class RegressedStreamRecoverySpec
    {
        private static readonly Account First = new Account("00112233-4455-4677-8899-aabbccddeeff", "0199f9a0-1111-7777-8888-999999999999", 0xa1);
        private long now = 100;

        [Fact]
        public async Task LostTerminalCommandsAreDemotedThenReplayedOnceInOrder()
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First) { Rejects = sequence => sequence == 3 };
            var db = await OpenSyncedAsync(files.Path, First, server, 3);
            await AdmitAsync(db, First, 4);
            server.RestoreBackup(1);
            var sender = Sender(db, First, server);
            Assert.Equal(SendCycleResult.OutcomeUncertain, await sender.RunOnceAsync(CancellationToken.None));
            var before = await ReadOutboxAsync(db, First);

            Assert.Equal(PrivateSyncResult.StreamRecovering, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            var demoted = await ReadOutboxAsync(db, First);
            Assert.Equal(new[] { "accepted", "pending", "pending", "in_flight" }, demoted.Select(row => row.State));
            Assert.Equal(before.Select(row => row.Identity), demoted.Select(row => row.Identity));
            Assert.Equal(before.Select(row => row.Result), demoted.Select(row => row.Result));
            Assert.Equal(1, await LongAsync(db, First, "SELECT finalized_through FROM gp_stream_state WHERE singleton=1"));
            Assert.Null(await new SqlitePrivateSyncStore(db, First.Scope, NoopProjector.Instance, _ => { }).GetBootstrapProgressAsync(CancellationToken.None));
            Assert.Equal(new[] { 2L, 3L }, server.LookedUp);

            now += 31_000;
            await DrainAsync(sender);
            Assert.Equal(PrivateSyncResult.ResetReconciled, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(new[] { 1L, 2L, 3L, 2L, 3L, 4L }, server.Executed);
            Assert.Equal(new[] { 1, 1, 0, 1 }, Enumerable.Range(1, 4).Select(server.GrantsFor));
            Assert.Equal(new[] { "accepted", "accepted", "terminal_rejected", "accepted" }, (await ReadOutboxAsync(db, First)).Select(row => row.State));
            Assert.Equal(4, await LongAsync(db, First, "SELECT finalized_through FROM gp_stream_state WHERE singleton=1"));
            Assert.Equal(new byte[] { 3 }, await BytesAsync(db, First, "SELECT payload FROM gp_confirmed_projection WHERE collection='wallet' AND entity_key='coins'"));
            Assert.Equal(SendCycleResult.Idle, await sender.RunOnceAsync(CancellationToken.None));
            Assert.Equal(PrivateSyncResult.BoundaryComplete, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));
            await DisposeAsync(db);
        }

        [Fact]
        public async Task CommandsTheServerKnowsByReceiptStayFinalizedAndAreNotResent()
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First);
            var db = await OpenSyncedAsync(files.Path, First, server, 3);
            var before = await ReadOutboxAsync(db, First);
            server.RotateEpoch();
            server.StaleBoundaries.Enqueue(1);

            Assert.Equal(PrivateSyncResult.ResetReconciled, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(new[] { 2L }, server.LookedUp);
            Assert.Equal(new[] { 1L, 2L, 3L }, server.Executed);
            Assert.Equal(before.Select(row => (row.Identity, row.State, row.Result == null ? -1 : row.Result[0])), (await ReadOutboxAsync(db, First)).Select(row => (row.Identity, row.State, row.Result == null ? -1 : row.Result[0])));
            Assert.Equal(3, await LongAsync(db, First, "SELECT finalized_through FROM gp_stream_state WHERE singleton=1"));
            await DisposeAsync(db);
        }

        [Fact]
        public async Task ConflictingReceiptBlocksRecoveryWithoutChangingLocalState()
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First);
            var db = await OpenSyncedAsync(files.Path, First, server, 3);
            await db.ExecuteAsync(First.Scope, t => ((SqliteTransactionSession)t).Execute("UPDATE gp_outbox SET delivery_state='terminal_rejected' WHERE sequence=2"), CancellationToken.None);
            var before = await ReadOutboxAsync(db, First);
            server.RotateEpoch();
            server.StaleBoundaries.Enqueue(1);

            var error = await Assert.ThrowsAsync<StorageException>(() => Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(StorageFailure.StreamRecoveryRequired, error.Failure);
            Assert.Equal(new[] { 2L }, server.LookedUp);
            Assert.Equal(before.Select(row => (row.Identity, row.State)), (await ReadOutboxAsync(db, First)).Select(row => (row.Identity, row.State)));
            Assert.Equal(3, await LongAsync(db, First, "SELECT finalized_through FROM gp_stream_state WHERE singleton=1"));
            await DisposeAsync(db);
        }

        [Fact]
        public async Task InterruptionAtEachRecoveryStepResumesOnTheNextCycle()
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First);
            var db = await OpenSyncedAsync(files.Path, First, server, 3);
            server.RestoreBackup(1);
            server.FailedLookups = 1;

            Assert.Equal(PrivateSyncResult.RemoteFailure, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));
            Assert.Equal(new[] { "accepted", "accepted", "accepted" }, (await ReadOutboxAsync(db, First)).Select(row => row.State));
            Assert.NotNull(await new SqlitePrivateSyncStore(db, First.Scope, NoopProjector.Instance, _ => { }).GetBootstrapProgressAsync(CancellationToken.None));

            var crashing = new SqlitePrivateSyncStore(db, First.Scope, NoopProjector.Instance, _ => { }, point => { if (point == SyncCheckpoint.BeforeDemotionCommit) throw new InjectedFailure(); });
            await Assert.ThrowsAsync<InjectedFailure>(() => new PrivateSyncCoordinator(crashing, server, server, () => now).PullOnceAsync(CancellationToken.None));
            Assert.Equal(new[] { "accepted", "accepted", "accepted" }, (await ReadOutboxAsync(db, First)).Select(row => row.State));
            Assert.Equal(3, await LongAsync(db, First, "SELECT finalized_through FROM gp_stream_state WHERE singleton=1"));
            await DisposeAsync(db);

            db = await OpenAsync(files.Path, First);
            Assert.Equal(PrivateSyncResult.StreamRecovering, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));
            Assert.Equal(SendCycleResult.Finalized, await Sender(db, First, server).RunOnceAsync(CancellationToken.None));
            await DisposeAsync(db);

            db = await OpenAsync(files.Path, First);
            Assert.Equal(PrivateSyncResult.ResetReconciled, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));
            Assert.Equal(new[] { "accepted", "accepted", "pending" }, (await ReadOutboxAsync(db, First)).Select(row => row.State));
            await DrainAsync(Sender(db, First, server));
            Assert.Equal(PrivateSyncResult.BoundaryComplete, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(new[] { 1L, 2L, 3L, 2L, 3L }, server.Executed);
            Assert.Equal(new[] { 1, 1, 1 }, Enumerable.Range(1, 3).Select(server.GrantsFor));
            Assert.Equal(new[] { "accepted", "accepted", "accepted" }, (await ReadOutboxAsync(db, First)).Select(row => row.State));
            await DisposeAsync(db);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReplayWithADifferentOutcomeReplacesTheOriginalResult(bool originallyAccepted)
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First) { Rejects = sequence => sequence == 2 && !originallyAccepted };
            var db = await OpenSyncedAsync(files.Path, First, server, 2);
            server.RestoreBackup(1);
            server.Rejects = sequence => sequence == 2 && originallyAccepted;

            Assert.Equal(PrivateSyncResult.StreamRecovering, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));
            var demoted = (await ReadOutboxAsync(db, First))[1];
            Assert.Equal("pending", demoted.State);
            Assert.Equal(new[] { originallyAccepted ? (byte)1 : (byte)2 }, demoted.Result);

            await DrainAsync(Sender(db, First, server));
            Assert.Equal(PrivateSyncResult.ResetReconciled, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            var replayed = (await ReadOutboxAsync(db, First))[1];
            Assert.Equal(originallyAccepted ? "terminal_rejected" : "accepted", replayed.State);
            Assert.Equal(new[] { originallyAccepted ? (byte)2 : (byte)1 }, replayed.Result);
            Assert.Equal(new[] { 1L, 2L, 2L }, server.Executed);
            Assert.Equal(originallyAccepted ? 0 : 1, server.GrantsFor(2));
            Assert.Equal(new[] { (byte)(originallyAccepted ? 1 : 2) }, await BytesAsync(db, First, "SELECT payload FROM gp_confirmed_projection WHERE collection='wallet' AND entity_key='coins'"));
            await DisposeAsync(db);
        }

        [Fact]
        public async Task ReceiptLookupConflictBlocksRecoveryWithoutChangingLocalState()
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First) { LookupFailure = new RemoteFailure(RemoteFailureKind.Conflict, 409) };
            var db = await OpenSyncedAsync(files.Path, First, server, 3);
            server.RestoreBackup(1);
            server.FailedLookups = 1;
            var before = await DurableStateAsync(db, First);

            var error = await Assert.ThrowsAsync<StorageException>(() => Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(StorageFailure.StreamRecoveryRequired, error.Failure);
            Assert.Equal(new[] { 2L }, server.LookedUp);
            Assert.Equal(before, await DurableStateAsync(db, First));
            await DisposeAsync(db);
        }

        [Fact]
        public async Task PersistentStaleBoundaryBlocksRecoveryWithoutChangingLocalState()
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First);
            var db = await OpenSyncedAsync(files.Path, First, server, 3);
            server.RotateEpoch();
            server.StaleBoundaries.Enqueue(1);
            server.StaleBoundaries.Enqueue(1);
            var before = await DurableStateAsync(db, First);

            var error = await Assert.ThrowsAsync<StorageException>(() => Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(StorageFailure.StreamRecoveryRequired, error.Failure);
            Assert.Equal(new[] { 2L, 2L }, server.LookedUp);
            Assert.Equal(before, await DurableStateAsync(db, First));
            await DisposeAsync(db);
        }

        [Fact]
        public async Task AbsentReceiptBelowTheStagedBoundaryRestartsTheBootstrap()
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First);
            var db = await OpenSyncedAsync(files.Path, First, server, 3);
            server.RestoreBackup(1);
            server.StaleBoundaries.Enqueue(2);

            Assert.Equal(PrivateSyncResult.StreamRecovering, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(new[] { 3L, 2L, 3L }, server.LookedUp);
            Assert.Equal(new[] { "accepted", "pending", "pending" }, (await ReadOutboxAsync(db, First)).Select(row => row.State));
            Assert.Equal(1, await LongAsync(db, First, "SELECT finalized_through FROM gp_stream_state WHERE singleton=1"));
            await DisposeAsync(db);
        }

        [Fact]
        public async Task PullFirstInstallAfterDemotionKeepsDemotedCommandsPendingUntilReplayed()
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First);
            var db = await OpenSyncedAsync(files.Path, First, server, 3);
            server.RestoreBackup(1);
            Assert.Equal(PrivateSyncResult.StreamRecovering, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(PrivateSyncResult.ResetReconciled, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(new[] { "accepted", "pending", "pending" }, (await ReadOutboxAsync(db, First)).Select(row => row.State));
            Assert.Equal(1, await LongAsync(db, First, "SELECT finalized_through FROM gp_stream_state WHERE singleton=1"));
            Assert.Equal(new[] { 1L, 2L, 3L }, server.Executed);

            await DrainAsync(Sender(db, First, server));
            Assert.Equal(PrivateSyncResult.BoundaryComplete, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(new[] { 1L, 2L, 3L, 2L, 3L }, server.Executed);
            Assert.Equal(new[] { 1, 1, 1 }, Enumerable.Range(1, 3).Select(server.GrantsFor));
            Assert.Equal(new[] { "accepted", "accepted", "accepted" }, (await ReadOutboxAsync(db, First)).Select(row => row.State));
            await DisposeAsync(db);
        }

        [Fact]
        public async Task DemotionLowersOnlyTheFinalizedSequence()
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First);
            var db = await OpenSyncedAsync(files.Path, First, server, 3);
            server.RestoreBackup(1);
            var before = await CountersAsync(db, First);

            Assert.Equal(PrivateSyncResult.StreamRecovering, await Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(before, await CountersAsync(db, First));
            Assert.Equal(1, await LongAsync(db, First, "SELECT finalized_through FROM gp_stream_state WHERE singleton=1"));
            await DisposeAsync(db);
        }

        [Fact]
        public async Task RetiredRegressedStreamIsNotDemoted()
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First);
            var db = await OpenSyncedAsync(files.Path, First, server, 2);
            server.RestoreBackup(1);
            server.Retired = true;

            var error = await Assert.ThrowsAsync<StorageException>(() => Coordinator(db, First, server).PullOnceAsync(CancellationToken.None));

            Assert.Equal(StorageFailure.StreamRecoveryRequired, error.Failure);
            Assert.Empty(server.LookedUp);
            Assert.Equal(new[] { "accepted", "accepted" }, (await ReadOutboxAsync(db, First)).Select(row => row.State));
            await DisposeAsync(db);
        }

        [Fact]
        public async Task CoordinatorWithoutReceiptsKeepsTheRegressedBoundaryBlocked()
        {
            using var files = new TemporaryDatabase();
            var server = new RestoredServer(First);
            var db = await OpenSyncedAsync(files.Path, First, server, 2);
            server.RestoreBackup(1);

            var error = await Assert.ThrowsAsync<StorageException>(() => new PrivateSyncCoordinator(Store(db, First), server, () => now).PullOnceAsync(CancellationToken.None));

            Assert.Equal(StorageFailure.StreamRecoveryRequired, error.Failure);
            Assert.Equal(new[] { "accepted", "accepted" }, (await ReadOutboxAsync(db, First)).Select(row => row.State));
            await DisposeAsync(db);
        }

        private PrivateSyncCoordinator Coordinator(SqliteDatabase db, Account account, RestoredServer server) => new PrivateSyncCoordinator(Store(db, account), server, server, () => now);

        private OrderedCommandSender Sender(SqliteDatabase db, Account account, RestoredServer server) => new OrderedCommandSender(new SqliteCommandDeliveryStore(db, account.Scope), server, () => now);

        private static SqlitePrivateSyncStore Store(SqliteDatabase db, Account account) => new SqlitePrivateSyncStore(db, account.Scope, NoopProjector.Instance, _ => { });

        private async Task<SqliteDatabase> OpenSyncedAsync(string path, Account account, RestoredServer server, int accepted)
        {
            var db = await OpenAsync(path, account);
            await db.ExecuteAsync(account.Scope, t => ((SqliteTransactionSession)t).Execute("INSERT INTO gp_stream_state VALUES (1,?,?,?,?,?,0,1,0,0)", account.Scope.BackendNamespace, account.Scope.AppId.Value, account.Scope.AccountId.Value, account.Stream.ToString(), account.Installation.ToString("D")), CancellationToken.None);
            Assert.Equal(PrivateSyncResult.BootstrapInstalled, await Coordinator(db, account, server).BootstrapAsync(account.Stream, CancellationToken.None));
            for (var sequence = 1; sequence <= accepted; sequence++) await AdmitAsync(db, account, sequence);
            await DrainAsync(Sender(db, account, server));
            Assert.Equal(PrivateSyncResult.BoundaryComplete, await Coordinator(db, account, server).PullOnceAsync(CancellationToken.None));
            return db;
        }

        private static Task<SqliteDatabase> OpenAsync(string path, Account account) => SqliteDatabase.OpenAsync(path, account.Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);

        private static Task<bool> AdmitAsync(SqliteDatabase db, Account account, long sequence) => db.ExecuteAsync(account.Scope, t =>
        {
            var s = (SqliteTransactionSession)t;
            s.Execute("INSERT INTO gp_outbox(operation_id,backend_namespace,app_id,account_id,client_stream_id,installation_id,sequence,business_run_id,operation_kind,schema_version,fingerprint_version,client_created_at,semantic_body,fingerprint,local_revision) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                account.Operation(sequence).ToString(), account.Scope.BackendNamespace, account.Scope.AppId.Value, account.Scope.AccountId.Value, account.Stream.ToString(), account.Installation.ToString("D"), sequence, "run-" + sequence, "profile.patch", 1, 1, 100L, new[] { (byte)sequence }, new[] { account.Marker, (byte)sequence }, sequence);
            s.Execute("UPDATE gp_stream_state SET next_sequence=?,local_revision=? WHERE singleton=1", sequence + 1, sequence);
            return true;
        }, CancellationToken.None);

        private static async Task DrainAsync(OrderedCommandSender sender)
        {
            while (await sender.RunOnceAsync(CancellationToken.None) == SendCycleResult.Finalized) { }
        }

        private static Task<IReadOnlyList<OutboxRow>> ReadOutboxAsync(SqliteDatabase db, Account account) => db.ExecuteAsync(account.Scope, t =>
        {
            var s = (SqliteTransactionSession)t;
            var rows = new List<OutboxRow>();
            var count = s.ExecuteScalar<long>("SELECT COUNT(*) FROM gp_outbox");
            for (var sequence = 1L; sequence <= count; sequence++)
            {
                const string row = " FROM gp_outbox WHERE sequence=?";
                var identity = string.Join("|", s.ExecuteScalar<string>("SELECT operation_id" + row, sequence), s.ExecuteScalar<string>("SELECT client_stream_id" + row, sequence),
                    Convert.ToBase64String(s.ExecuteScalar<byte[]>("SELECT semantic_body" + row, sequence)), Convert.ToBase64String(s.ExecuteScalar<byte[]>("SELECT fingerprint" + row, sequence)));
                var hasResult = s.ExecuteScalar<int>("SELECT COUNT(*)" + row + " AND terminal_result IS NOT NULL", sequence) == 1;
                rows.Add(new OutboxRow(identity, s.ExecuteScalar<string>("SELECT delivery_state" + row, sequence), hasResult ? s.ExecuteScalar<byte[]>("SELECT terminal_result" + row, sequence) : null));
            }
            return (IReadOnlyList<OutboxRow>)rows;
        }, CancellationToken.None);

        private static async Task<string> DurableStateAsync(SqliteDatabase db, Account account)
        {
            var rows = await ReadOutboxAsync(db, account);
            var outbox = rows.Select(row => row.Identity + "|" + row.State + "|" + (row.Result == null ? "-" : Convert.ToBase64String(row.Result)));
            var finalized = await LongAsync(db, account, "SELECT finalized_through FROM gp_stream_state WHERE singleton=1");
            return string.Join(";", outbox) + ";" + finalized + ";" + await CountersAsync(db, account);
        }

        private static Task<string> CountersAsync(SqliteDatabase db, Account account) => db.ExecuteAsync(account.Scope, t =>
        {
            var s = (SqliteTransactionSession)t;
            return string.Join("|",
                s.ExecuteScalar<long>("SELECT next_sequence FROM gp_stream_state WHERE singleton=1"),
                s.ExecuteScalar<long>("SELECT local_revision FROM gp_stream_state WHERE singleton=1"),
                s.ExecuteScalar<long>("SELECT visibility_generation FROM gp_sync_state WHERE singleton=1"),
                s.ExecuteScalar<long>("SELECT committed_through FROM gp_sync_state WHERE singleton=1"),
                Convert.ToBase64String(s.ExecuteScalar<byte[]>("SELECT pull_cursor FROM gp_sync_state WHERE singleton=1")));
        }, CancellationToken.None);

        private static Task<long> LongAsync(SqliteDatabase db, Account account, string sql) => db.ExecuteAsync(account.Scope, t => ((SqliteTransactionSession)t).ExecuteScalar<long>(sql), CancellationToken.None);

        private static Task<byte[]> BytesAsync(SqliteDatabase db, Account account, string sql) => db.ExecuteAsync(account.Scope, t => ((SqliteTransactionSession)t).ExecuteScalar<byte[]>(sql), CancellationToken.None);

        private static async Task DisposeAsync(SqliteDatabase db) => Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));

        private static byte[] Token(int value)
        {
            var token = new byte[12];
            Array.Fill(token, (byte)value);
            return token;
        }

        private sealed class OutboxRow
        {
            public OutboxRow(string identity, string state, byte[]? result) { Identity = identity; State = state; Result = result; }
            public string Identity { get; }
            public string State { get; }
            public byte[]? Result { get; }
        }

        private sealed class Account
        {
            public Account(string accountId, string stream, byte marker)
            {
                Scope = new StorageScope("test-backend", new PlatformId("01890f3e-7a6b-7c8d-9e0f-102030405060"), new PlatformId(accountId));
                Stream = new ClientStreamId(Guid.Parse(stream));
                Installation = Guid.Parse(stream.Substring(0, 9) + "1212" + stream.Substring(13));
                Marker = marker;
            }
            public StorageScope Scope { get; }
            public ClientStreamId Stream { get; }
            public Guid Installation { get; }
            public byte Marker { get; }
            public OperationId Operation(long sequence) => new OperationId(Guid.Parse($"{Marker:x2}99f9a0-cccc-7777-8888-{sequence:D12}"));
        }

        /// <summary>
        /// One account's ordered stream whose database can be restored from an older backup.
        /// </summary>
        private sealed class RestoredServer : IPrivateSyncRemote, ICommandRemote, ICommandReceiptRemote
        {
            private readonly Account account;
            private readonly List<(OperationId Operation, bool Accepted)> finalized = new List<(OperationId, bool)>();
            private readonly Dictionary<OperationId, int> grants = new Dictionary<OperationId, int>();
            private int epoch = 1;
            private long snapshotThrough;

            public RestoredServer(Account account) => this.account = account;

            public Func<long, bool> Rejects { get; set; } = _ => false;
            public Queue<long> StaleBoundaries { get; } = new Queue<long>();
            public int FailedLookups { get; set; }
            public RemoteFailure LookupFailure { get; set; } = new RemoteFailure(RemoteFailureKind.Unavailable, 503);
            public bool Retired { get; set; }
            public List<long> Executed { get; } = new List<long>();
            public List<long> LookedUp { get; } = new List<long>();

            public int GrantsFor(int sequence) => grants.TryGetValue(account.Operation(sequence), out var count) ? count : 0;

            public void RestoreBackup(long through)
            {
                while (finalized.Count > through)
                {
                    var lost = finalized[finalized.Count - 1];
                    if (lost.Accepted) grants[lost.Operation]--;
                    finalized.RemoveAt(finalized.Count - 1);
                }
                RotateEpoch();
            }

            public void RotateEpoch() => epoch++;

            public Task<RemoteResult<BootstrapStart>> StartBootstrapAsync(ClientStreamId streamId, CancellationToken cancellationToken)
            {
                Assert.Equal(account.Stream, streamId);
                long through = StaleBoundaries.Count > 0 ? StaleBoundaries.Dequeue() : finalized.Count;
                snapshotThrough = Head();
                var collections = new[] { "profile", "progression", "inventory", "wallet", "entitlements" }.Select(name => new SnapshotCollection(name, 1, true)).ToArray();
                return Task.FromResult(RemoteResult<BootstrapStart>.Success(new BootstrapStart(streamId, Retired ? "retired" : "active", through, Retired ? (long?)null : through + 1,
                    snapshotThrough, 1, Guid.Parse($"0199f9a0-aaaa-7777-8888-{epoch:D12}"), collections, Token(epoch), Token(100 + epoch), 10_000_000)));
            }

            public Task<RemoteResult<BootstrapPage>> GetBootstrapPageAsync(byte[] session, byte[] pageToken, int maximumBytes, CancellationToken cancellationToken)
            {
                var coins = new RemoteProjectionMutation("wallet", "coins", snapshotThrough, ProjectionMutationKind.Upsert, new[] { (byte)grants.Values.Sum() });
                return Task.FromResult(RemoteResult<BootstrapPage>.Success(new BootstrapPage(session, snapshotThrough, new[] { coins }, false, null, Token(200 + epoch))));
            }

            public Task<RemoteResult<RemotePullPage>> PullAsync(byte[] cursor, int maximumBytes, CancellationToken cancellationToken) =>
                Task.FromResult(RemoteResult<RemotePullPage>.Success(cursor.SequenceEqual(Token(200 + epoch))
                    ? RemotePullPage.Page(Head(), Array.Empty<RemotePullGroup>(), cursor, false)
                    : RemotePullPage.Reset("log_epoch_changed")));

            public Task<RemoteResult<RemoteCommandOutcome>> SendAsync(RemoteCommand command, CancellationToken cancellationToken)
            {
                Assert.Equal(account.Stream, command.StreamId);
                if (command.Sequence <= finalized.Count)
                    return Task.FromResult(Recorded(command));
                if (command.Sequence != finalized.Count + 1)
                    return Task.FromResult(RemoteResult<RemoteCommandOutcome>.Failed(new RemoteFailure(RemoteFailureKind.Conflict, 200)));
                var accepted = !Rejects(command.Sequence);
                finalized.Add((command.OperationId, accepted));
                Executed.Add(command.Sequence);
                if (accepted) grants[command.OperationId] = (grants.TryGetValue(command.OperationId, out var count) ? count : 0) + 1;
                return Task.FromResult(RemoteResult<RemoteCommandOutcome>.Success(Outcome(accepted)));
            }

            public Task<RemoteResult<RemoteCommandReceipt>> LookupAsync(RemoteCommand command, CancellationToken cancellationToken)
            {
                LookedUp.Add(command.Sequence);
                if (FailedLookups > 0)
                {
                    FailedLookups--;
                    return Task.FromResult(RemoteResult<RemoteCommandReceipt>.Failed(LookupFailure));
                }
                if (command.Sequence > finalized.Count)
                    return Task.FromResult(RemoteResult<RemoteCommandReceipt>.Success(new RemoteCommandReceipt(false, null, finalized.Count)));
                var recorded = Recorded(command);
                return Task.FromResult(recorded.IsSuccess
                    ? RemoteResult<RemoteCommandReceipt>.Success(new RemoteCommandReceipt(true, recorded.Value, finalized.Count))
                    : RemoteResult<RemoteCommandReceipt>.Failed(recorded.Failure));
            }

            private long Head() => 100L * epoch + finalized.Count;

            private RemoteResult<RemoteCommandOutcome> Recorded(RemoteCommand command)
            {
                var recorded = finalized[(int)command.Sequence - 1];
                return recorded.Operation == command.OperationId
                    ? RemoteResult<RemoteCommandOutcome>.Success(Outcome(recorded.Accepted))
                    : RemoteResult<RemoteCommandOutcome>.Failed(new RemoteFailure(RemoteFailureKind.Conflict, 409));
            }

            private static RemoteCommandOutcome Outcome(bool accepted) => new RemoteCommandOutcome(accepted ? RemoteCommandStatus.Accepted : RemoteCommandStatus.TerminalRejected, new[] { accepted ? (byte)1 : (byte)2 });
        }

        private sealed class NoopProjector : IPrivateSyncProjectionProjector
        {
            public static readonly NoopProjector Instance = new NoopProjector();
            public void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group) { }
            public void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot) { }
        }

        private sealed class InjectedFailure : Exception { }

        private sealed class TemporaryDatabase : IDisposable
        {
            private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "game-platform-regressed-stream", Guid.NewGuid().ToString("N"));
            public TemporaryDatabase() { Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory, "platform.sqlite3"); }
            public string Path { get; }
            public void Dispose() { try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch (IOException) { } }
        }
    }
}
