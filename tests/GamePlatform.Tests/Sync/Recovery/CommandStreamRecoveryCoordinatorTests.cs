using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Abstractions.Recovery;
using GamePlatform.Storage.Abstractions.Sync;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Features.Accounts;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Recovery;
using GamePlatform.Storage.Sqlite.Sync;
using GamePlatform.Sync.Pull;
using GamePlatform.Sync.Recovery;
using RemoteRequest = GamePlatform.Backend.Contracts.Remote.CommandStreamRecoveryRequest;

namespace GamePlatform.Tests.Sync.Recovery
{
    public sealed class CommandStreamRecoveryCoordinatorTests
    {
        [Theory]
        [InlineData(CommandStreamRecoveryReason.LocalDatabaseRecreated)]
        [InlineData(CommandStreamRecoveryReason.BackupRestored)]
        [InlineData(CommandStreamRecoveryReason.StreamIdentityConflict)]
        public async Task LostResponseAndRestartReuseIdentityThenCompleteRealSqliteBootstrap(CommandStreamRecoveryReason reason)
        {
            await using var f = await Fixture.OpenAsync(reason == CommandStreamRecoveryReason.LocalDatabaseRecreated);
            f.Server.LoseResponse = true;
            var coordinator = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.OutcomeUncertain, await coordinator.RecoverAsync(reason, CancellationToken.None));
            Assert.Equal(CommandStreamRecoveryPhase.Prepared, (await f.JournalAsync()).Phase);
            Assert.Equal(f.Prior, (await f.EntryAsync()).StreamId);
            Assert.Equal(1, f.Server.Commits);
            await coordinator.StopAsync();
            await f.ReopenDirectoryAsync();
            f.Server.TokenExpired = true;
            f.Host.FailBootstrap = true;
            coordinator = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.BootstrapRequired, await coordinator.ResumeAsync(CancellationToken.None));
            Assert.Equal(CommandStreamRecoveryPhase.Adopted, (await f.JournalAsync()).Phase);
            Assert.Equal(f.Server.Requests[0].NewStreamId, (await f.EntryAsync()).StreamId);
            Assert.Equal(1, f.Server.Commits);
            Assert.Equal(1, f.Server.Inspections);
            Assert.Equal(f.Server.Requests[0].RecoveryId, f.Server.Requests[1].RecoveryId);
            Assert.Equal(f.Server.Requests[0].InspectionToken, f.Server.Requests[1].InspectionToken);
            await coordinator.StopAsync();
            await f.ReopenDirectoryAsync();
            f.Host.FailBootstrap = false;
            coordinator = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.BootstrapComplete, await coordinator.ResumeAsync(CancellationToken.None));
            Assert.Equal((await f.EntryAsync()).StreamId, f.Host.LastQuiesced);
            Assert.Equal(CommandStreamRecoveryPhase.BootstrapComplete, (await f.JournalAsync()).Phase);
            Assert.Equal(1, await f.SuccessorLongAsync("SELECT next_sequence FROM gp_stream_state"));
            Assert.Equal(0, await f.SuccessorLongAsync("SELECT COUNT(*) FROM gp_outbox"));
            Assert.Equal(1, await f.SuccessorLongAsync("SELECT ready FROM gp_stream_state"));
            Assert.Equal(71, await f.SuccessorLongAsync("SELECT committed_through FROM gp_sync_state"));
            // Rechecking a completed journal must use real bootstrap again and must not rotate.
            Assert.Equal(CommandStreamRecoveryOutcome.BootstrapComplete, await coordinator.ResumeAsync(CancellationToken.None));
            Assert.Equal((await f.EntryAsync()).StreamId, f.Host.LastQuiesced);
            Assert.Equal(1, f.Server.Commits);
            await coordinator.StopAsync();
        }

        [Fact]
        public async Task HealthyStreamAndMissingJournalResumeDoNothing()
        {
            await using var f = await Fixture.OpenAsync();
            f.Host.DamageEstablished = false;
            var c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.DamageNotEstablished, await c.ResumeAsync(CancellationToken.None));
            Assert.Equal(CommandStreamRecoveryOutcome.DamageNotEstablished, await c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, CancellationToken.None));
            Assert.Equal(0, f.Server.Inspections);
            Assert.Equal(1, await f.PriorLongAsync("SELECT ready FROM gp_stream_state"));
            Assert.Equal(f.Prior, (await f.EntryAsync()).StreamId);
            await c.StopAsync();
        }

        [Fact]
        public async Task AbsentReceiptBlocksRotationAndRetainsIntentUntilRealReceiptExists()
        {
            await using var f = await Fixture.OpenAsync();
            await f.InsertCommandAsync();
            var c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.UnresolvedCommands, await c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, CancellationToken.None));
            Assert.Equal(0, f.Server.Inspections);
            Assert.Equal(1, await f.PriorLongAsync("SELECT COUNT(*) FROM gp_outbox WHERE delivery_state='pending'"));
            Assert.Equal(0, await f.PriorLongAsync("SELECT ready FROM gp_stream_state"));
            Assert.Equal(1, await f.PriorLongAsync("SELECT COUNT(*) FROM gp_outbox WHERE semantic_body=X'01' AND fingerprint=X'02'"));
            f.Server.ReceiptFound = true;
            Assert.Equal(CommandStreamRecoveryOutcome.BootstrapComplete, await c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, CancellationToken.None));
            Assert.Equal(1, await f.PriorLongAsync("SELECT COUNT(*) FROM gp_outbox WHERE delivery_state='accepted'"));
            Assert.Equal(0, await f.PriorLongAsync("SELECT finalized_through FROM gp_stream_state"));
            Assert.Equal(0, await f.SuccessorLongAsync("SELECT COUNT(*) FROM gp_outbox"));
            Assert.Equal(1, f.Server.Commits);
            await c.StopAsync();
        }

        [Fact]
        public async Task RetirementDuringRemoteCallbackLeavesPreparedRequestWithoutAdoption()
        {
            await using var f = await Fixture.OpenAsync();
            f.Server.HoldResponse = true;
            var c = await f.CoordinatorAsync();
            var run = c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, CancellationToken.None);
            await f.Server.RemoteEntered.Task;
            var stopped = c.StopAsync();
            f.Server.ReleaseResponse.TrySetResult(true);
            Assert.Equal(CommandStreamRecoveryOutcome.Retired, await run);
            await stopped;
            Assert.Equal(f.Prior, (await f.EntryAsync()).StreamId);
            Assert.Equal(CommandStreamRecoveryPhase.Prepared, (await f.JournalAsync()).Phase);
            Assert.Equal(CommandStreamRecoveryOutcome.Retired, await c.ResumeAsync(CancellationToken.None));
        }

        [Fact]
        public async Task OfflineRetryDoesNotMintAnotherRequestAndWrongInstallationIsRejected()
        {
            await using var f = await Fixture.OpenAsync();
            f.Server.Offline = true;
            var c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.RemoteFailure, await c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, CancellationToken.None));
            var id = (await f.JournalAsync()).Request.RecoveryId;
            f.Server.Offline = false;
            Assert.Equal(CommandStreamRecoveryOutcome.BootstrapComplete, await c.ResumeAsync(CancellationToken.None));
            Assert.All(f.Server.Requests, r => Assert.Equal(id, r.RecoveryId));
            await Assert.ThrowsAsync<StorageException>(() => new SqliteRecoveryCommandStore(f.PriorDb, f.Scope, Guid.Parse("0199f9a0-aaaa-7777-8888-999999999999")).SuspendAndReadAsync(f.Prior, CancellationToken.None));
            await c.StopAsync();
        }

        [Theory]
        [InlineData("INSERT INTO gp_bootstrap_entities VALUES ('wallet','coins',1,X'01')")]
        [InlineData("INSERT INTO gp_bootstrap_pages VALUES (X'01',NULL,X'02')")]
        [InlineData("INSERT INTO gp_feature_state VALUES ('view','wallet','coins',1,100,X'01',X'02')")]
        [InlineData("CREATE TABLE consumer_retained_intents (identity TEXT)")]
        public async Task RecreatedDatabaseWithRetainedOrUnknownStateFailsClosed(string statement)
        {
            await using var f = await Fixture.OpenAsync(true);
            await f.PriorDb.ExecuteAsync(f.Scope, t => ((SqliteTransactionSession)t).Execute(statement), CancellationToken.None);
            var c = await f.CoordinatorAsync();
            await Assert.ThrowsAsync<StorageException>(() => c.RecoverAsync(CommandStreamRecoveryReason.LocalDatabaseRecreated, CancellationToken.None));
            Assert.Equal(0, f.Server.Inspections);
            Assert.Equal(f.Prior, (await f.EntryAsync()).StreamId);
            await c.StopAsync();
        }

        [Fact]
        public async Task BootstrapCommittedBeforeJournalCompletionCrashResumesByQuiescingSuccessor()
        {
            await using var f = await Fixture.OpenAsync();
            using (var injection = new SQLite.SQLiteConnection(Path.Combine(f.Folder, "directory.db")))
                injection.Execute("CREATE TRIGGER fail_bootstrap_complete BEFORE UPDATE OF phase ON gp_command_stream_recovery WHEN NEW.phase=4 BEGIN SELECT RAISE(ABORT,'crash boundary'); END");
            var c = await f.CoordinatorAsync();
            await Assert.ThrowsAsync<StorageException>(() => c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, CancellationToken.None));
            Assert.Equal(CommandStreamRecoveryPhase.Adopted, (await f.JournalAsync()).Phase);
            Assert.Equal(1, await f.SuccessorLongAsync("SELECT ready FROM gp_stream_state"));
            await c.StopAsync();
            using (var injection = new SQLite.SQLiteConnection(Path.Combine(f.Folder, "directory.db")))
                injection.Execute("DROP TRIGGER fail_bootstrap_complete");
            await f.ReopenDirectoryAsync();
            c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.BootstrapComplete, await c.ResumeAsync(CancellationToken.None));
            Assert.Equal((await f.EntryAsync()).StreamId, f.Host.LastQuiesced);
            Assert.Equal(1, f.Server.Commits);
            await c.StopAsync();
        }

        [Fact]
        public async Task ExpiredUncommittedRequestIsRetainedAndResumeReinspectsWithOriginalReason()
        {
            await using var f = await Fixture.OpenAsync();
            f.Server.Offline = true;
            var c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.RemoteFailure, await c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, CancellationToken.None));
            var first = (await f.JournalAsync()).Request;
            f.Server.Offline = false;
            f.Server.TokenExpired = true;
            Assert.Equal(CommandStreamRecoveryOutcome.RemoteFailure, await c.ResumeAsync(CancellationToken.None));
            Assert.Equal(CommandStreamRecoveryPhase.Expired, (await f.JournalAsync()).Phase);
            await c.StopAsync();
            await f.ReopenDirectoryAsync();
            c = await f.CoordinatorAsync();
            f.Server.TokenExpired = false;
            Assert.Equal(CommandStreamRecoveryOutcome.BootstrapComplete, await c.ResumeAsync(CancellationToken.None));
            Assert.Equal(2, f.Server.Inspections);
            Assert.NotEqual(first.RecoveryId, f.Server.Requests.Last().RecoveryId);
            Assert.NotEqual(first.SuccessorStreamId, f.Server.Requests.Last().NewStreamId);
            Assert.Equal(CommandStreamRecoveryReason.BackupRestored, f.Server.Requests.Last().Reason);
            await c.StopAsync();
        }

        private sealed class Fixture : IAsyncDisposable
        {
            public readonly string Folder = Path.Combine(Path.GetTempPath(), "gp-stream-recovery", Guid.NewGuid().ToString("N"));
            public readonly AppId App = new AppId(Guid.Parse("0199f9a0-1111-7777-8888-999999999999"));
            public readonly PlatformUserId Account = new PlatformUserId(Guid.Parse("0199f9a0-2222-7777-8888-999999999999"));
            public readonly Guid Installation = Guid.Parse("0199f9a0-3333-7777-8888-999999999999");
            public readonly ClientStreamId Prior = new ClientStreamId(Guid.Parse("0199f9a0-4444-7777-8888-999999999999"));
            public readonly AccountPrincipalDescriptor Principal = new AccountPrincipalDescriptor(new BackendNamespace("recovery-test"), "issuer", "subject");
            public StorageScope Scope => new StorageScope("recovery-test", new PlatformId(App.ToString()), new PlatformId(Account.ToString()));
            public AccountDirectoryScope DirectoryScope => new AccountDirectoryScope(Principal.BackendNamespace, App);
            public SqliteAccountsDirectoryDatabase DirectoryDb = null!;
            public SqliteDatabase PriorDb = null!;
            public SqliteDatabase? SuccessorDb;
            public Server Server = null!;
            public Host Host = null!;
            private bool recreated;
            private int ids;

            public static async Task<Fixture> OpenAsync(bool recreated = false)
            {
                var f = new Fixture { recreated = recreated };
                Directory.CreateDirectory(f.Folder);
                f.DirectoryDb = await SqliteAccountsDirectoryDatabase.OpenAsync(Path.Combine(f.Folder, "directory.db"), f.DirectoryScope, CancellationToken.None);
                var directory = new SqliteAccountsDirectoryStore(f.DirectoryDb, f.DirectoryScope);
                var entry = await directory.ReserveAsync(f.Principal, f.App, f.Installation, f.Prior, CancellationToken.None);
                await directory.BindIssuedAccountAsync(entry, f.Account, "active", CancellationToken.None);
                f.PriorDb = await f.OpenGameAsync(Path.Combine(f.Folder, "prior.db"));
                if (!recreated)
                    await f.InitializeStreamAsync(f.PriorDb, f.Prior, true);
                f.Server = new Server(f);
                f.Host = new Host(f);
                return f;
            }
            public Task<SqliteDatabase> OpenGameAsync(string path) => SqliteDatabase.OpenAsync(path, Scope, SqlitePlatformMigrationRegistry.Migrations, CancellationToken.None);
            public Task InitializeStreamAsync(SqliteDatabase db, ClientStreamId stream, bool ready) => db.ExecuteAsync(Scope, t => ((SqliteTransactionSession)t).Execute("INSERT INTO gp_stream_state(singleton,backend_namespace,app_id,account_id,client_stream_id,installation_id,ready,next_sequence,local_revision,finalized_through) VALUES (1,?,?,?,?,?,?,1,0,0)", Scope.BackendNamespace, App.ToString(), Account.ToString(), stream.ToString(), Installation.ToString("D"), ready ? 1 : 0), CancellationToken.None);
            public async Task<AccountDirectoryEntry> EntryAsync() => await new SqliteAccountsDirectoryStore(DirectoryDb, DirectoryScope).FindAsync(Principal, App, CancellationToken.None) ?? throw new InvalidOperationException();
            public async Task<CommandStreamRecoveryRecord> JournalAsync() => await new SqliteCommandStreamRecoveryJournal(DirectoryDb, await EntryAsync()).LoadAsync(CancellationToken.None) ?? throw new InvalidOperationException();
            public async Task<CommandStreamRecoveryCoordinator> CoordinatorAsync() => new CommandStreamRecoveryCoordinator(await EntryAsync(), Server, Server,
                new SqliteCommandStreamRecoveryJournal(DirectoryDb, await EntryAsync()), new SqliteRecoveryCommandStore(PriorDb, Scope, Installation, recreated), Host,
                () => Guid.Parse($"0199f9a1-{++ids:x4}-7777-8888-999999999999"));
            public async Task ReopenDirectoryAsync()
            {
                Assert.True(await DirectoryDb.DisposeAsync(TimeSpan.FromSeconds(5)));
                DirectoryDb = await SqliteAccountsDirectoryDatabase.OpenAsync(Path.Combine(Folder, "directory.db"), DirectoryScope, CancellationToken.None);
            }
            public Task<long> PriorLongAsync(string sql) => PriorDb.ExecuteAsync(Scope, t => ((SqliteTransactionSession)t).ExecuteScalar<long>(sql), CancellationToken.None);
            public Task<long> SuccessorLongAsync(string sql) => SuccessorDb!.ExecuteAsync(Scope, t => ((SqliteTransactionSession)t).ExecuteScalar<long>(sql), CancellationToken.None);
            public Task InsertCommandAsync() => PriorDb.ExecuteAsync(Scope, t =>
            {
                var s = (SqliteTransactionSession)t;
                s.Execute("INSERT INTO gp_outbox(operation_id,backend_namespace,app_id,account_id,client_stream_id,installation_id,sequence,business_run_id,operation_kind,schema_version,fingerprint_version,client_created_at,semantic_body,fingerprint,local_revision) VALUES (?,?,?,?,?,?,1,'run','profile.patch',1,1,100,X'01',X'02',1)", "0199f9a0-5555-7777-8888-999999999999", Scope.BackendNamespace, App.ToString(), Account.ToString(), Prior.ToString(), Installation.ToString("D"));
                s.Execute("UPDATE gp_stream_state SET next_sequence=2,local_revision=1");
                return true;
            }, CancellationToken.None);
            public async ValueTask DisposeAsync()
            {
                if (SuccessorDb != null)
                    Assert.True(await SuccessorDb.DisposeAsync(TimeSpan.FromSeconds(5)));
                Assert.True(await PriorDb.DisposeAsync(TimeSpan.FromSeconds(5)));
                Assert.True(await DirectoryDb.DisposeAsync(TimeSpan.FromSeconds(5)));
                Directory.Delete(Folder, true);
            }
        }

        private sealed class Host : ICommandStreamRecoveryHost
        {
            private readonly Fixture f;
            public Host(Fixture fixture)
            {
                f = fixture;
            }
            public bool IsCurrentAccount { get; set; } = true;
            public bool DamageEstablished = true;
            public bool FailBootstrap;
            public ClientStreamId LastQuiesced;
            public Task<bool> QuiesceAsync(ClientStreamId stream, CommandStreamRecoveryReason reason, bool resume, CancellationToken ct)
            {
                LastQuiesced = stream;
                return Task.FromResult(resume || DamageEstablished);
            }
            public async Task<bool> BootstrapSuccessorAsync(ClientStreamId stream, CancellationToken ct)
            {
                if (FailBootstrap)
                    return false;
                Assert.Equal(stream, (await f.EntryAsync()).StreamId);
                if (f.SuccessorDb == null)
                {
                    f.SuccessorDb = await f.OpenGameAsync(Path.Combine(f.Folder, "successor.db"));
                    await f.InitializeStreamAsync(f.SuccessorDb, stream, false);
                }
                var store = new SqlitePrivateSyncStore(f.SuccessorDb, f.Scope, new Projector(), _ => { });
                return await new PrivateSyncCoordinator(store, f.Server, () => 100).BootstrapAsync(stream, ct) == PrivateSyncResult.BootstrapInstalled;
            }
        }

        private sealed class Server : ICommandStreamRecoveryRemote, ICommandReceiptRemote, IPrivateSyncRemote
        {
            private readonly Fixture f;
            public Server(Fixture fixture)
            {
                f = fixture;
            }
            public readonly List<RemoteRequest> Requests = new List<RemoteRequest>();
            public int Inspections, Commits;
            public bool LoseResponse, Offline, ReceiptFound, TokenExpired, HoldResponse;
            public readonly TaskCompletionSource<bool> RemoteEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> ReleaseResponse = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<RemoteResult<CommandStreamInspection>> InspectAsync(ClientStreamId stream, CancellationToken ct)
            {
                Inspections++;
                return Task.FromResult(RemoteResult<CommandStreamInspection>.Success(new CommandStreamInspection(stream, f.Installation, 41, 42, CommandStreamState.Active, "abcdefghijklmnop", 101, 100)));
            }
            public async Task<RemoteResult<CommandStreamRecoveryResult>> RecoverAsync(RemoteRequest request, CancellationToken ct)
            {
                Requests.Add(request);
                if (Offline)
                    return RemoteResult<CommandStreamRecoveryResult>.Failed(new RemoteFailure(RemoteFailureKind.NotSent));
                if (Commits == 0 && TokenExpired)
                    return RemoteResult<CommandStreamRecoveryResult>.Failed(new RemoteFailure(RemoteFailureKind.InspectionExpired, 409));
                if (Commits == 0)
                {
                    Assert.False(TokenExpired);
                    Commits++;
                }
                else
                    Assert.Equal(Requests[0].RecoveryId, request.RecoveryId);
                if (HoldResponse)
                {
                    RemoteEntered.TrySetResult(true);
                    await ReleaseResponse.Task;
                }
                if (LoseResponse)
                {
                    LoseResponse = false;
                    return RemoteResult<CommandStreamRecoveryResult>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain));
                }
                return RemoteResult<CommandStreamRecoveryResult>.Success(new CommandStreamRecoveryResult(request.PriorStreamId, request.NewStreamId, request.RecoveryId, request.ExpectedFinalizedThrough, 1, 102));
            }
            public Task<RemoteResult<RemoteCommandReceipt>> LookupAsync(RemoteCommand command, CancellationToken ct) => Task.FromResult(RemoteResult<RemoteCommandReceipt>.Success(new RemoteCommandReceipt(ReceiptFound, ReceiptFound ? new RemoteCommandOutcome(RemoteCommandStatus.Accepted, new byte[] { 3 }) : null, 41)));
            public Task<RemoteResult<BootstrapStart>> StartBootstrapAsync(ClientStreamId stream, CancellationToken ct) => Task.FromResult(RemoteResult<BootstrapStart>.Success(new BootstrapStart(stream, "active", 0, 1, 71, 1, Guid.Parse("0199f9a2-1111-7777-8888-999999999999"), new[] { "profile", "progression", "inventory", "wallet", "entitlements" }.Select(n => new SnapshotCollection(n, 1, true)).ToArray(), new byte[12], new byte[12], 1000)));
            public Task<RemoteResult<BootstrapPage>> GetBootstrapPageAsync(byte[] session, byte[] page, int maximumBytes, CancellationToken ct) => Task.FromResult(RemoteResult<BootstrapPage>.Success(new BootstrapPage(session, 71, Array.Empty<RemoteProjectionMutation>(), false, null, Enumerable.Repeat((byte)7, 12).ToArray())));
            public Task<RemoteResult<RemotePullPage>> PullAsync(byte[] cursor, int maximumBytes, CancellationToken ct) => throw new NotSupportedException();
        }

        private sealed class Projector : IPrivateSyncProjectionProjector
        {
            public void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot)
            {
            }
            public void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group)
            {
            }
        }
    }
}
