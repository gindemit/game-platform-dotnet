#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Abstractions.Recovery;
using GamePlatform.Storage.Abstractions.Sync;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Features.Accounts;
using GamePlatform.Storage.Sqlite.Migrations;
using GamePlatform.Storage.Sqlite.Outbox;
using GamePlatform.Storage.Sqlite.Recovery;
using GamePlatform.Storage.Sqlite.Sync;
using GamePlatform.Sync.Pull;
using GamePlatform.Sync.Recovery;
using GamePlatform.Transport.Http;
using GamePlatform.Wire.Contracts;
using CommandStreamRecoveryRequest = GamePlatform.Backend.Contracts.Remote.CommandStreamRecoveryRequest;

namespace GamePlatform.Tests.Sync.Recovery
{
    /// <summary>Opt-in native PostgreSQL/HTTPS qualification. Ordinary test runs explicitly skip it.</summary>
    public sealed class RecoveryQualificationFactAttribute : FactAttribute
    {
        public RecoveryQualificationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GP_RECOVERY_FIXTURE_CONFIG")))
                Skip = "Requires owned native PostgreSQL HTTPS fixture; see tests/Qualification/Recovery/README.md.";
        }
    }
    public sealed class RecoveryChildFactAttribute : FactAttribute
    {
        public RecoveryChildFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GP_RECOVERY_CHILD_FOLDER")))
                Skip = "Process death child; invoked only by the qualification parent.";
        }
    }

    [CollectionDefinition("Joined recovery", DisableParallelization = true)]
    public sealed class JoinedRecoveryCollection { }

    [Collection("Joined recovery")]
    public sealed class JoinedCommandStreamRecoveryTests
    {
        [RecoveryQualificationFact]
        public async Task ActualProcessDeathAfterRemoteCommitAndAdoptionResumesSameDurableRequest()
        {
            foreach (var barrier in new[] { "remote-commit", "adopted" })
            {
                await using var f = await Fixture.OpenAsync();
                var economy = await f.EconomyAsync();
                if (barrier == "remote-commit") await f.ControlAsync("hold-next-recovery");
                await f.CloseDatabasesAsync();
                var assembly = typeof(JoinedCommandStreamRecoveryTests).Assembly.Location;
                var start = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var arg in new[] { "vstest", assembly, "--TestCaseFilter:FullyQualifiedName=GamePlatform.Tests.Sync.Recovery.JoinedCommandStreamRecoveryTests.ProcessRecoveryChild" }) start.ArgumentList.Add(arg);
                start.Environment["GP_RECOVERY_CHILD_FOLDER"] = f.Folder;
                start.Environment["GP_RECOVERY_CHILD_ACCOUNT"] = f.Account.ToString();
                start.Environment["GP_RECOVERY_CHILD_BARRIER"] = barrier;
                using var child = Process.Start(start)!;
                var stdout = child.StandardOutput.ReadToEndAsync();
                var stderr = child.StandardError.ReadToEndAsync();
                try
                {
                    if (barrier == "remote-commit") await f.WaitForRecoveryCommitAsync();
                    else
                    {
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                        while (!File.Exists(Path.Combine(f.Folder, "adopted.barrier")))
                        {
                            Assert.False(child.HasExited, "Child exited before adoption barrier: " + (child.HasExited ? await stdout + await stderr : ""));
                            await Task.Delay(25, timeout.Token);
                        }
                    }
                    Assert.False(child.HasExited);
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync();
                    Assert.NotEqual(0, child.ExitCode);
                }
                finally
                {
                    if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); }
                    if (barrier == "remote-commit") await f.ControlAsync("release");
                    await stdout; await stderr;
                    await f.OpenDatabasesAsync();
                }
                var interrupted = await f.JournalAsync();
                Assert.Equal(barrier == "remote-commit" ? CommandStreamRecoveryPhase.Prepared : CommandStreamRecoveryPhase.Adopted, interrupted.Phase);
                await f.ControlAsync("expire-inspections");
                var c = await f.CoordinatorAsync();
                Assert.Equal(CommandStreamRecoveryOutcome.BootstrapComplete, await c.ResumeAsync(default));
                await c.StopAsync();
                await f.ReopenAsync();
                await f.AssertBootstrapAsync(interrupted.Request.SuccessorStreamId);
                await f.AssertSingleSuccessorAsync(interrupted.Request.SuccessorStreamId);
                Assert.Equal(economy, await f.EconomyAsync());
                using var evidence = await f.EvidenceAsync();
                Assert.Equal(1, evidence.RootElement.GetProperty("inspections").GetInt32());
                var attempts = evidence.RootElement.GetProperty("recoveries").EnumerateArray().ToArray();
                Assert.Equal(barrier == "remote-commit" ? 2 : 1, attempts.Length);
                Assert.All(attempts, attempt => Assert.Equal(attempts[0].GetProperty("requestBodySha256").GetString(), attempt.GetProperty("requestBodySha256").GetString()));
            }
        }

        [RecoveryChildFact]
        public async Task ProcessRecoveryChild()
        {
            await using var f = await Fixture.AttachAsync(Environment.GetEnvironmentVariable("GP_RECOVERY_CHILD_FOLDER")!,
                new PlatformUserId(Guid.Parse(Environment.GetEnvironmentVariable("GP_RECOVERY_CHILD_ACCOUNT")!)));
            f.Host.PauseAfterAdoption = Environment.GetEnvironmentVariable("GP_RECOVERY_CHILD_BARRIER") == "adopted";
            var c = await f.CoordinatorAsync();
            await c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, default);
            throw new InvalidOperationException("Qualification parent must terminate this child at its durable barrier.");
        }
        [RecoveryQualificationFact]
        public async Task LostCommittedResponseReopensAndRetriesExpiredImmutableRequestThenBootstraps()
        {
            await using var f = await Fixture.OpenAsync();
            var economy = await f.EconomyAsync();
            await f.ControlAsync("drop-next-recovery");
            var c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.OutcomeUncertain, await c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, default));
            var prepared = await f.JournalAsync();
            Assert.Equal(CommandStreamRecoveryPhase.Prepared, prepared.Phase);
            Assert.Equal(f.Prior, (await f.EntryAsync()).StreamId);
            await c.StopAsync();
            await f.ReopenAsync();
            await f.ControlAsync("expire-inspections");
            f.Host.InterruptBootstrap = true;
            c = await f.CoordinatorAsync();
            await Assert.ThrowsAsync<StorageException>(() => c.ResumeAsync(default));
            Assert.Equal(CommandStreamRecoveryPhase.Adopted, (await f.JournalAsync()).Phase);
            Assert.Equal(prepared.Request.SuccessorStreamId, (await f.EntryAsync()).StreamId);
            Assert.Equal(0, await f.SuccessorLongAsync("SELECT ready FROM gp_stream_state"));
            await c.StopAsync();
            await f.ReopenAsync();
            f.Host.InterruptBootstrap = false;
            c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.BootstrapComplete, await c.ResumeAsync(default));
            await c.StopAsync();
            await f.ReopenAsync();
            await f.AssertBootstrapAsync(prepared.Request.SuccessorStreamId);
            using (var e = await f.EvidenceAsync())
            {
                var attempts = e.RootElement.GetProperty("recoveries").EnumerateArray().ToArray();
                Assert.Equal(2, attempts.Length);
                Assert.Equal(attempts[0].GetProperty("requestBodySha256").GetString(), attempts[1].GetProperty("requestBodySha256").GetString());
                Assert.Equal(1, e.RootElement.GetProperty("inspections").GetInt32());
            }
            Assert.Equal(economy, await f.EconomyAsync());
            await f.ControlAsync("retry-grant-on-successor");
            Assert.Equal(economy, await f.EconomyAsync());
            await f.AssertSingleSuccessorAsync(prepared.Request.SuccessorStreamId);
        }

        [RecoveryQualificationFact]
        public async Task RemoteCommitBeforeAdoptionCrashReopensWithoutAnotherRotation()
        {
            await using var f = await Fixture.OpenAsync();
            f.DirectoryTrigger("CREATE TRIGGER fail_adopt BEFORE UPDATE OF phase ON gp_command_stream_recovery WHEN NEW.phase=3 BEGIN SELECT RAISE(ABORT,'qualification adoption interruption'); END");
            var c = await f.CoordinatorAsync();
            await Assert.ThrowsAsync<StorageException>(() => c.RecoverAsync(CommandStreamRecoveryReason.StreamIdentityConflict, default));
            var committed = await f.JournalAsync();
            Assert.Equal(CommandStreamRecoveryPhase.RemoteCommitted, committed.Phase);
            Assert.Equal(f.Prior, (await f.EntryAsync()).StreamId);
            await c.StopAsync();
            f.DirectoryTrigger("DROP TRIGGER fail_adopt");
            await f.ReopenAsync();
            c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.BootstrapComplete, await c.ResumeAsync(default));
            await c.StopAsync();
            await f.ReopenAsync();
            await f.AssertBootstrapAsync(committed.Request.SuccessorStreamId);
            await f.AssertSingleSuccessorAsync(committed.Request.SuccessorStreamId);
            using var e = await f.EvidenceAsync();
            Assert.Single(e.RootElement.GetProperty("recoveries").EnumerateArray());
        }

        [RecoveryQualificationFact]
        public async Task BootstrapCommittedBeforeJournalCompletionCrashRevalidatesDurableSuccessor()
        {
            await using var f = await Fixture.OpenAsync();
            f.DirectoryTrigger("CREATE TRIGGER fail_complete BEFORE UPDATE OF phase ON gp_command_stream_recovery WHEN NEW.phase=4 BEGIN SELECT RAISE(ABORT,'qualification completion interruption'); END");
            var c = await f.CoordinatorAsync();
            await Assert.ThrowsAsync<StorageException>(() => c.RecoverAsync(CommandStreamRecoveryReason.LocalDatabaseRecreated, default));
            var adopted = await f.JournalAsync();
            Assert.Equal(CommandStreamRecoveryPhase.Adopted, adopted.Phase);
            Assert.Equal(1, await f.SuccessorLongAsync("SELECT ready FROM gp_stream_state"));
            await c.StopAsync();
            f.DirectoryTrigger("DROP TRIGGER fail_complete");
            await f.ReopenAsync();
            c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.BootstrapComplete, await c.ResumeAsync(default));
            Assert.Equal(adopted.Request.SuccessorStreamId, f.Host.LastQuiesced);
            await c.StopAsync();
            await f.ReopenAsync();
            await f.AssertBootstrapAsync(adopted.Request.SuccessorStreamId);
            await f.AssertSingleSuccessorAsync(adopted.Request.SuccessorStreamId);
        }

        [RecoveryQualificationFact]
        public async Task AbsentActualReceiptBlocksRotationAndPreservesImmutableCommandAfterReopen()
        {
            await using var f = await Fixture.OpenAsync();
            await f.InsertUnresolvedAsync();
            var c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.UnresolvedCommands, await c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, default));
            await c.StopAsync();
            await f.ReopenAsync();
            Assert.Equal(f.Prior, (await f.EntryAsync()).StreamId);
            await f.AssertPendingIntentAsync();
            Assert.Equal(0, await f.PriorLongAsync("SELECT ready FROM gp_stream_state"));
            Assert.Equal(3, await f.PriorLongAsync("SELECT next_sequence FROM gp_stream_state"));
            using var e = await f.EvidenceAsync();
            Assert.Empty(e.RootElement.GetProperty("recoveries").EnumerateArray());
            Assert.Equal(0, e.RootElement.GetProperty("inspections").GetInt32());
        }

        [RecoveryQualificationFact]
        public async Task HealthyOwnerAndUnprovenDamagePreserveDatabaseAndRemoteStream()
        {
            await using var f = await Fixture.OpenAsync();
            await f.InsertUnresolvedAsync();
            f.Host.DamageEstablished = false;
            var c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.DamageNotEstablished, await c.ResumeAsync(default));
            Assert.Equal(CommandStreamRecoveryOutcome.DamageNotEstablished, await c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, default));
            await c.StopAsync();
            await f.ReopenAsync();
            Assert.Equal(f.Prior, (await f.EntryAsync()).StreamId);
            Assert.Equal(1, await f.PriorLongAsync("SELECT ready FROM gp_stream_state"));
            await f.AssertPendingIntentAsync();
            using var e = await f.EvidenceAsync();
            Assert.Empty(e.RootElement.GetProperty("recoveries").EnumerateArray());
        }

        [RecoveryQualificationFact]
        public async Task RetirementAfterRealCommitFencesLateResponseAndRetainsPreparedJournal()
        {
            await using var f = await Fixture.OpenAsync();
            await f.ControlAsync("hold-next-recovery");
            var c = await f.CoordinatorAsync();
            var run = c.RecoverAsync(CommandStreamRecoveryReason.BackupRestored, default);
            await f.WaitForRecoveryCommitAsync();
            var stop = c.StopAsync();
            await f.ControlAsync("release");
            Assert.Equal(CommandStreamRecoveryOutcome.Retired, await run);
            await stop;
            await f.ReopenAsync();
            Assert.Equal(f.Prior, (await f.EntryAsync()).StreamId);
            Assert.Equal(CommandStreamRecoveryPhase.Prepared, (await f.JournalAsync()).Phase);
            Assert.Equal(CommandStreamRecoveryOutcome.Retired, await c.ResumeAsync(default));
            c = await f.CoordinatorAsync();
            Assert.Equal(CommandStreamRecoveryOutcome.BootstrapComplete, await c.ResumeAsync(default));
            await c.StopAsync();
            await f.ReopenAsync();
            await f.AssertBootstrapAsync((await f.EntryAsync()).StreamId);
        }

        [RecoveryQualificationFact]
        public async Task WatermarkAdvanceAndOwnerAppInstallationBindingRejectStaleRecovery()
        {
            await using var f = await Fixture.OpenAsync();
            var inspected = await f.Remote.InspectAsync(f.Prior, default);
            Assert.True(inspected.IsSuccess);
            var request = new CommandStreamRecoveryRequest(f.Prior, new ClientStreamId(Guid.Parse("0199f9a1-1111-7777-8888-999999999999")), Guid.Parse("0199f9a1-2222-7777-8888-999999999999"), CommandStreamRecoveryReason.BackupRestored,
                inspected.Value!.InspectionToken, inspected.Value.FinalizedThrough);
            await f.ControlAsync("advance-watermark");
            Assert.Equal(RemoteFailureKind.Conflict, (await f.Remote.RecoverAsync(request, default)).Failure.Kind);
            var fresh = await f.Remote.InspectAsync(f.Prior, default);
            Assert.True(fresh.IsSuccess);
            Assert.True(fresh.Value!.FinalizedThrough > inspected.Value.FinalizedThrough);
            request = new CommandStreamRecoveryRequest(f.Prior, request.NewStreamId, request.RecoveryId, request.Reason, fresh.Value.InspectionToken, fresh.Value.FinalizedThrough);
            Assert.False((await f.Provider("fixture-other", f.App, f.Installation).RecoverAsync(request, default)).IsSuccess);
            Assert.False((await f.Provider("fixture-owner", new AppId(Guid.NewGuid()), f.Installation).RecoverAsync(request, default)).IsSuccess);
            Assert.False((await f.Provider("fixture-owner", f.App, Guid.NewGuid()).RecoverAsync(request, default)).IsSuccess);
            Assert.True((await f.Remote.RecoverAsync(request, default)).IsSuccess);
            await f.AssertSingleSuccessorAsync(request.NewStreamId);
        }

        private sealed class Fixture : IAsyncDisposable
        {
            public readonly AppId App = new AppId(Guid.Parse("018f2400-0000-7000-8000-000000000001"));
            public PlatformUserId Account;
            public readonly Guid Installation = Guid.Parse("0199f9a0-3333-7777-8888-999999999999");
            public readonly ClientStreamId Prior = new ClientStreamId(Guid.Parse("0199f9a0-4444-7777-8888-999999999999"));
            public readonly AccountPrincipalDescriptor Principal = new AccountPrincipalDescriptor(new BackendNamespace("recovery-qualification"), "https://fin-recovery.invalid", "fixture-owner");
            public StorageScope Scope => new StorageScope("recovery-qualification", new PlatformId(App.ToString()), new PlatformId(Account.ToString()));
            public AccountDirectoryScope DirectoryScope => new AccountDirectoryScope(Principal.BackendNamespace, App);
            public readonly string Folder;
            private readonly bool ownsFolder;
            private bool databasesOpen;
            private Fixture(string? folder = null)
            { Folder = folder ?? Path.Combine(Path.GetTempPath(), "gp-joined-recovery", Guid.NewGuid().ToString("N")); ownsFolder = folder == null; }
            public SqliteAccountsDirectoryDatabase DirectoryDb = null!;
            public SqliteDatabase PriorDb = null!;
            public SqliteDatabase? SuccessorDb;
            public CommandStreamRecoveryHttpProvider Remote = null!;
            public CommandReceiptHttpProvider Receipts = null!;
            public PrivateSyncHttpProvider Sync = null!;
            public Host Host = null!;
            private HttpClient client = null!;
            private BackendHttpConfiguration configuration = null!;
            private BoundedHttpClientExecutor executor = null!;
            private readonly MessagePackWireCodec codec = new MessagePackWireCodec();
            private readonly AuthRefreshCoordinator refresh = new AuthRefreshCoordinator();
            private int ids;
            private byte[] pendingBody = Array.Empty<byte>();
            private byte[] pendingFingerprint = Array.Empty<byte>();
            public static async Task<Fixture> OpenAsync()
            {
                var f = new Fixture();
                f.ConfigureHttp();
                await f.ControlAsync("reset");
                var provision = await new ProvisioningHttpProvider(f.configuration, f.executor, f.codec, new Auth("fixture-owner"), f.refresh)
                    .ProvisionAsync(f.App, f.Installation, f.Prior, default);
                Assert.True(provision.IsSuccess, "Real production provision must succeed.");
                f.Account = provision.Value!.AccountId;
                f.ConfigureProviders();
                await f.ControlAsync("grant-once");
                Directory.CreateDirectory(f.Folder);
                await f.OpenDatabasesAsync();
                var directory = new SqliteAccountsDirectoryStore(f.DirectoryDb, f.DirectoryScope);
                var entry = await directory.ReserveAsync(f.Principal, f.App, f.Installation, f.Prior, default);
                await directory.BindIssuedAccountAsync(entry, f.Account, "active", default);
                await f.InitializeStreamAsync(f.PriorDb, f.Prior, true);
                return f;
            }
            public static async Task<Fixture> AttachAsync(string folder, PlatformUserId account)
            {
                var f = new Fixture(folder) { Account = account };
                f.ConfigureHttp();
                f.ConfigureProviders();
                await f.OpenDatabasesAsync();
                return f;
            }
            private void ConfigureHttp()
            {
                using var config = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("GP_RECOVERY_FIXTURE_CONFIG")!));
                var origin = new Uri(config.RootElement.GetProperty("baseUri").GetString()!);
                Assert.Equal("https", origin.Scheme);
                Assert.True(origin.IsLoopback);
                var pin = config.RootElement.GetProperty("certificateSha256").GetString()!.Replace(":", "");
                var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
                handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
                    request.RequestUri != null && request.RequestUri.IsLoopback && request.RequestUri.Scheme == "https" &&
                    request.RequestUri.Host == origin.Host && request.RequestUri.Port == origin.Port && cert != null &&
                    string.Equals(Convert.ToHexString(SHA256.HashData(cert.RawData)), pin, StringComparison.OrdinalIgnoreCase);
                client = new HttpClient(handler) { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(20) };
                configuration = new BackendHttpConfiguration(origin, Principal.BackendNamespace);
                executor = new BoundedHttpClientExecutor(client, configuration);
            }
            private void ConfigureProviders()
            {
                Remote = Provider("fixture-owner", App, Installation);
                Receipts = new CommandReceiptHttpProvider(App, Account, Installation, configuration, executor, codec, new Auth("fixture-owner"), refresh);
                Sync = new PrivateSyncHttpProvider(App, Account, configuration, executor, codec, new Auth("fixture-owner"), refresh);
                Host = new Host(this);
            }
            public CommandStreamRecoveryHttpProvider Provider(string token, AppId app, Guid installation) =>
                new CommandStreamRecoveryHttpProvider(app, Account, installation, configuration, executor, codec, new Auth(token), refresh);
            public async Task ControlAsync(string action)
            {
                using var content = new StringContent(JsonSerializer.Serialize(new { action }), Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("qualification/control", content);
                Assert.True(response.IsSuccessStatusCode, "Fixture control failed: " + action + " " + await response.Content.ReadAsStringAsync());
            }
            public async Task<JsonDocument> EvidenceAsync() => JsonDocument.Parse(await client.GetStringAsync("qualification/evidence"));
            public async Task<string> EconomyAsync()
            {
                using var e = await EvidenceAsync();
                Assert.True(e.RootElement.GetProperty("rewardFulfillments").GetInt32() > 0);
                Assert.NotEmpty(e.RootElement.GetProperty("wallets").EnumerateArray());
                return e.RootElement.GetProperty("rewardFulfillments").GetRawText() + "|" + e.RootElement.GetProperty("wallets").GetRawText();
            }
            public Task<SqliteDatabase> OpenGameAsync(string name) => SqliteDatabase.OpenAsync(Path.Combine(Folder, name), Scope, SqlitePlatformMigrationRegistry.Migrations, default);
            public Task InitializeStreamAsync(SqliteDatabase db, ClientStreamId stream, bool ready) => db.ExecuteAsync(Scope, t => ((SqliteTransactionSession)t).Execute(
                "INSERT INTO gp_stream_state(singleton,backend_namespace,app_id,account_id,client_stream_id,installation_id,ready,next_sequence,local_revision,finalized_through) VALUES (1,?,?,?,?,?,?,1,0,0)",
                Scope.BackendNamespace, App.ToString(), Account.ToString(), stream.ToString(), Installation.ToString("D"), ready ? 1 : 0), default);
            public async Task<AccountDirectoryEntry> EntryAsync() => await new SqliteAccountsDirectoryStore(DirectoryDb, DirectoryScope).FindAsync(Principal, App, default) ?? throw new InvalidOperationException();
            public async Task<CommandStreamRecoveryRecord> JournalAsync() => await new SqliteCommandStreamRecoveryJournal(DirectoryDb, await EntryAsync()).LoadAsync(default) ?? throw new InvalidOperationException();
            public async Task<CommandStreamRecoveryCoordinator> CoordinatorAsync() => new CommandStreamRecoveryCoordinator(await EntryAsync(), Remote, Receipts,
                new SqliteCommandStreamRecoveryJournal(DirectoryDb, await EntryAsync()), new SqliteRecoveryCommandStore(PriorDb, Scope, Installation), Host,
                () => Guid.Parse($"0199f9a1-{++ids:x4}-7777-8888-999999999999"));
            public void DirectoryTrigger(string sql) { using var connection = new SQLite.SQLiteConnection(Path.Combine(Folder, "directory.db")); connection.Execute(sql); }
            public async Task ReopenAsync()
            {
                await CloseDatabasesAsync();
                await OpenDatabasesAsync();
            }
            public async Task CloseDatabasesAsync()
            {
                if (!databasesOpen) return;
                Assert.True(await DirectoryDb.DisposeAsync(TimeSpan.FromSeconds(5)));
                Assert.True(await PriorDb.DisposeAsync(TimeSpan.FromSeconds(5)));
                if (SuccessorDb != null) Assert.True(await SuccessorDb.DisposeAsync(TimeSpan.FromSeconds(5)));
                databasesOpen = false;
            }
            public async Task OpenDatabasesAsync()
            {
                DirectoryDb = await SqliteAccountsDirectoryDatabase.OpenAsync(Path.Combine(Folder, "directory.db"), DirectoryScope, default);
                PriorDb = await OpenGameAsync("prior.db");
                if (File.Exists(Path.Combine(Folder, "successor.db"))) SuccessorDb = await OpenGameAsync("successor.db");
                databasesOpen = true;
            }
            public Task<long> PriorLongAsync(string sql) => PriorDb.ExecuteAsync(Scope, t => ((SqliteTransactionSession)t).ExecuteScalar<long>(sql), default);
            public Task<long> SuccessorLongAsync(string sql) => SuccessorDb!.ExecuteAsync(Scope, t => ((SqliteTransactionSession)t).ExecuteScalar<long>(sql), default);
            public async Task InsertUnresolvedAsync()
            {
                // Seed the local baseline at the genuinely finalized grant; the next intent is not sent.
                await PriorDb.ExecuteAsync(Scope, t =>
                {
                    var s = (SqliteTransactionSession)t;
                    s.Execute("UPDATE gp_stream_state SET next_sequence=2,finalized_through=1");
                    s.Execute("CREATE TABLE qualification_profile_intent(display_name TEXT, local_revision INTEGER)");
                    return true;
                }, default);
                var owner = new OwnerScope(Principal.BackendNamespace, App, Account);
                pendingBody = codec.Encode(new ProfilePatchCommand(0, WireOptional<string>.Present("Retained intent"), default, default));
                var operation = new OperationId(Guid.Parse("0199f9a0-5555-7777-8888-999999999999"));
                var admitted = await new SqliteAtomicCommandStore(PriorDb, Scope, owner, new CanonicalCommandFingerprint(), new FixedClock())
                    .CommitAsync("qualification-profile-intent", new CommandDraft(owner, operation, Prior, "profile.patch", 1, 1, pendingBody),
                        (t, revision) => ((SqliteTransactionSession)t).Execute("INSERT INTO qualification_profile_intent VALUES ('Retained intent',?)", revision), default);
                Assert.Equal(2, admitted.Sequence);
                pendingFingerprint = new byte[32];
                new CanonicalCommandFingerprint().Compute(owner, operation, Prior, Installation, 2, "profile.patch", 1, 1, admitted.ClientCreatedAt, pendingBody, pendingFingerprint);
                await AssertPendingIntentAsync();
            }
            public async Task AssertPendingIntentAsync()
            {
                Assert.Equal(1, await PriorDb.ExecuteAsync(Scope, t => ((SqliteTransactionSession)t).ExecuteScalar<long>(
                    "SELECT COUNT(*) FROM gp_outbox WHERE sequence=2 AND delivery_state='pending' AND semantic_body=? AND fingerprint=? AND operation_kind='profile.patch'", pendingBody, pendingFingerprint), default));
                Assert.Equal(1, await PriorLongAsync("SELECT COUNT(*) FROM qualification_profile_intent WHERE display_name='Retained intent' AND local_revision=1"));
            }
            public async Task AssertBootstrapAsync(ClientStreamId successor)
            {
                Assert.Equal(successor, (await EntryAsync()).StreamId);
                Assert.Equal(CommandStreamRecoveryPhase.BootstrapComplete, (await JournalAsync()).Phase);
                Assert.Equal(1, await SuccessorLongAsync("SELECT ready FROM gp_stream_state"));
                Assert.Equal(1, await SuccessorLongAsync("SELECT next_sequence FROM gp_stream_state"));
                Assert.Equal(0, await SuccessorLongAsync("SELECT finalized_through FROM gp_stream_state"));
                Assert.Equal(0, await SuccessorLongAsync("SELECT COUNT(*) FROM gp_outbox"));
                Assert.Equal(1, await SuccessorLongAsync("SELECT COUNT(*) FROM gp_sync_state WHERE ready=1 AND length(pull_cursor)>0 AND committed_through>=0 AND fixed_through IS NULL"));
                Assert.True(await SuccessorLongAsync("SELECT COUNT(*) FROM gp_confirmed_projection WHERE state='visible' AND length(payload)>0") > 0);
                Assert.True(await SuccessorLongAsync("SELECT COUNT(*) FROM gp_confirmed_projection WHERE collection='wallet'") > 0);
                var walletBytes = await SuccessorDb!.ExecuteAsync(Scope, t => ((SqliteTransactionSession)t).ExecuteScalar<byte[]>("SELECT payload FROM gp_confirmed_projection WHERE collection='wallet' AND entity_key='test.coin'"), default);
                Assert.Equal(1, codec.Decode<ProjectionWalletSnapshot>(walletBytes).Data.Balance);
                Assert.Equal(0, await SuccessorLongAsync("SELECT COUNT(*) FROM gp_bootstrap_state"));
                var checkpoint = await new SqlitePrivateSyncStore(SuccessorDb!, Scope, new SnapshotProjector(), _ => { }).GetPullCheckpointAsync(default);
                Assert.Equal(successor, checkpoint.StreamId);
                Assert.True(checkpoint.CopyCursor().Length >= 12);
                var started = await Sync.StartBootstrapAsync(successor, default);
                Assert.True(started.IsSuccess);
                Assert.Equal(started.Value!.CommittedThrough, checkpoint.CommittedThrough);
            }
            public async Task AssertSingleSuccessorAsync(ClientStreamId successor)
            {
                using var evidence = await EvidenceAsync();
                var streams = evidence.RootElement.GetProperty("streams").EnumerateArray().ToArray();
                Assert.Equal(2, streams.Length);
                Assert.Single(streams, s => s.GetProperty("state").GetString() == "active" && s.GetProperty("clientStreamId").GetString() == successor.ToString());
                Assert.Single(streams, s => s.GetProperty("state").GetString() == "retired" && s.GetProperty("clientStreamId").GetString() == Prior.ToString());
                Assert.Single(evidence.RootElement.GetProperty("committedRecoveries").EnumerateArray());
            }
            public async Task WaitForRecoveryCommitAsync()
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (true)
                {
                    using var e = await EvidenceAsync();
                    if (e.RootElement.GetProperty("streams").EnumerateArray().Any(s => s.GetProperty("state").GetString() == "retired")) return;
                    await Task.Delay(20, timeout.Token);
                }
            }
            public async ValueTask DisposeAsync()
            {
                await CloseDatabasesAsync();
                client.Dispose();
                refresh.Dispose();
                if (ownsFolder) Directory.Delete(Folder, true);
            }
        }

        private sealed class Host : ICommandStreamRecoveryHost
        {
            private readonly Fixture f;
            public Host(Fixture fixture) { f = fixture; }
            public bool IsCurrentAccount { get; set; } = true;
            public bool DamageEstablished = true;
            public bool InterruptBootstrap;
            public bool PauseAfterAdoption;
            public ClientStreamId LastQuiesced;
            public Task<bool> QuiesceAsync(ClientStreamId stream, CommandStreamRecoveryReason reason, bool resume, CancellationToken ct)
            { LastQuiesced = stream; return Task.FromResult(resume || DamageEstablished); }
            public async Task<bool> BootstrapSuccessorAsync(ClientStreamId stream, CancellationToken ct)
            {
                Assert.Equal(stream, (await f.EntryAsync()).StreamId);
                if (PauseAfterAdoption)
                {
                    await File.WriteAllTextAsync(Path.Combine(f.Folder, "adopted.barrier"), stream.ToString(), ct);
                    await Task.Delay(Timeout.Infinite, ct);
                }
                if (f.SuccessorDb == null)
                {
                    f.SuccessorDb = await f.OpenGameAsync("successor.db");
                    await f.InitializeStreamAsync(f.SuccessorDb, stream, false);
                }
                var store = new SqlitePrivateSyncStore(f.SuccessorDb, f.Scope, new SnapshotProjector(), _ => { },
                    checkpoint => { if (InterruptBootstrap && checkpoint == SyncCheckpoint.BeforeBootstrapInstall) throw new StorageException(StorageFailure.NotReady, "qualification bootstrap interruption"); });
                return await new PrivateSyncCoordinator(store, f.Sync, () => 1726704000000).BootstrapAsync(stream, ct) == PrivateSyncResult.BootstrapInstalled;
            }
        }
        // Portable confirmed projection is written by the production SqlitePrivateSyncStore.
        // Consumer-specific projection/admission implementations remain a separate Unity lane.
        private sealed class SnapshotProjector : IPrivateSyncProjectionProjector
        {
            public void ReplaceConfirmedSnapshot(ILocalStorageTransaction transaction, IReadOnlyList<StoredProjectionMutation> snapshot)
            {
                Assert.NotEmpty(snapshot);
                Assert.Contains(snapshot, x => x.Collection == "wallet");
            }
            public void ProjectConfirmedGroup(ILocalStorageTransaction transaction, StoredPullGroup group) { }
        }
        private sealed class Auth : IAuthSession
        {
            private readonly string token;
            public Auth(string token) { this.token = token; }
            public string SessionKey => token;
            public Task<AccessTokenSnapshot> GetAsync(CancellationToken ct) => Task.FromResult(new AccessTokenSnapshot(token, 0));
            public Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken ct) => GetAsync(ct);
        }
        private sealed class FixedClock : IUnixMillisecondClock
        { public long GetUnixMilliseconds() => 1726704000000; }
    }
}
