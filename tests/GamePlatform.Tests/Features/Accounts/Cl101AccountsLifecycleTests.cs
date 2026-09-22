#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Accounts;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Sqlite.Executor;
using GamePlatform.Storage.Sqlite.Features.Accounts;
using GamePlatform.Storage.Sqlite.Migrations;

namespace GamePlatform.Tests.Features.Accounts
{
    public sealed class Cl101AccountsLifecycleTests
    {
        private static readonly AppId App = new AppId(Guid.Parse("0199f9a0-1111-7777-8888-999999999999"));
        private static readonly AccountPrincipalDescriptor A = new AccountPrincipalDescriptor(new BackendNamespace("test-backend"), "issuer", "subject-a");
        private static readonly AccountPrincipalDescriptor B = new AccountPrincipalDescriptor(new BackendNamespace("test-backend"), "issuer", "subject-b");

        [Fact]
        public async Task LostProvisioningResponseReusesDurableReservationAndIssuedAccount()
        {
            var directory = new Directory(); var remote = new Remote { FailFirst = true }; var service = Service(new Auth(A), remote, directory, new Leases());
            Assert.Equal(AccountsReadiness.Unavailable, (await service.StartAsync(App, CancellationToken.None)).Readiness);
            var reservation = await directory.FindAsync(A, App, CancellationToken.None); Assert.NotNull(reservation); Assert.False(reservation!.HasIssuedAccount);
            Assert.Equal(AccountsReadiness.Ready, (await service.StartAsync(App, CancellationToken.None)).Readiness);
            var issued = await directory.FindAsync(A, App, CancellationToken.None); Assert.Equal(reservation.InstallationId, issued!.InstallationId); Assert.Equal(reservation.StreamId, issued.StreamId); Assert.True(issued.HasIssuedAccount); Assert.Equal(2, remote.Calls);
        }

        [Fact]
        public async Task PartialBootstrapNeverBecomesReadyAndOfflineKnownAccountRetainsReservation()
        {
            var directory = new Directory(); var leases = new Leases { Bootstrap = AccountBootstrapResult.Incomplete }; var service = Service(new Auth(A), new Remote(), directory, leases);
            var first = await service.StartAsync(App, CancellationToken.None); Assert.Equal(AccountsReadiness.Bootstrapping, first.Readiness); Assert.NotNull(first.Entry);
            var offline = Service(new Auth(AccountAuthLifecycleResult.UnavailableOffline(A)), new Remote(), directory, new Leases { Ready = false });
            var reopened = await offline.StartAsync(App, CancellationToken.None); Assert.Equal(AccountsReadiness.UnavailableOffline, reopened.Readiness); Assert.Equal(first.Entry!.StreamId, reopened.Entry!.StreamId);
        }

        [Fact]
        public async Task MissingKnownCredentialsRequiresRecoveryAndDoesNotProvision()
        {
            var remote = new Remote(); var service = Service(new Auth(AccountAuthLifecycleResult.RecoveryRequired(A)), remote, new Directory(), new Leases());
            Assert.Equal(AccountsReadiness.RecoveryRequired, (await service.StartAsync(App, CancellationToken.None)).Readiness); Assert.Equal(0, remote.Calls);
        }

        [Fact]
        public async Task ProvisioningFactoryReceivesTheExactAuthenticatedSessionUsedForTheAccountScope()
        {
            var session = new Session(); var remote = new Remote(); var factory = new ProvisioningFactory(_ => remote); var leases = new Leases();
            var service = Service(new Auth(AccountAuthLifecycleResult.Authenticated(A, session)), factory, new Directory(), leases);
            Assert.Equal(AccountsReadiness.Ready, (await service.StartAsync(App, CancellationToken.None)).Readiness);
            Assert.Equal(1, factory.Calls); Assert.Same(session, factory.LastSession); Assert.Same(session, leases.LastAuthenticatedSession);
        }

        [Fact]
        public async Task ProvisioningFactoryIsNotUsedForKnownOfflineRecoveryOrCancellation()
        {
            var directory = new Directory(); var initialFactory = new ProvisioningFactory(_ => new Remote());
            var initial = Service(new Auth(A), initialFactory, directory, new Leases());
            Assert.Equal(AccountsReadiness.Ready, (await initial.StartAsync(App, CancellationToken.None)).Readiness);

            var offlineFactory = new ProvisioningFactory(_ => new Remote());
            var offline = Service(new Auth(AccountAuthLifecycleResult.UnavailableOffline(A)), offlineFactory, directory, new Leases());
            Assert.Equal(AccountsReadiness.Ready, (await offline.StartAsync(App, CancellationToken.None)).Readiness);
            Assert.Equal(0, offlineFactory.Calls);

            var recoveryFactory = new ProvisioningFactory(_ => new Remote());
            var recovery = Service(new Auth(AccountAuthLifecycleResult.RecoveryRequired(A)), recoveryFactory, directory, new Leases());
            Assert.Equal(AccountsReadiness.RecoveryRequired, (await recovery.StartAsync(App, CancellationToken.None)).Readiness);
            Assert.Equal(0, recoveryFactory.Calls);

            var authCancelledFactory = new ProvisioningFactory(_ => new Remote());
            var authCancelled = Service(new Auth(AccountAuthLifecycleResult.Cancelled()), authCancelledFactory, directory, new Leases());
            Assert.Equal(AccountsReadiness.Cancelled, (await authCancelled.StartAsync(App, CancellationToken.None)).Readiness);
            Assert.Equal(0, authCancelledFactory.Calls);

            var cancellationFactory = new ProvisioningFactory(_ => new Remote()); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var cancellation = Service(new Auth(A), cancellationFactory, new Directory(), new Leases());
            Assert.Equal(AccountsReadiness.Cancelled, (await cancellation.StartAsync(App, cancelled.Token)).Readiness);
            Assert.Equal(0, cancellationFactory.Calls);
        }

        [Fact]
        public async Task StopWhileAuthenticationIsPendingPreventsProvisioningFactoryCreation()
        {
            var authenticated = new TaskCompletionSource<AccountAuthLifecycleResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var factory = new ProvisioningFactory(_ => new Remote());
            var service = Service(new PendingAuth(authenticated.Task), factory, new Directory(), new Leases());
            var starting = service.StartAsync(App, CancellationToken.None);
            Assert.Equal(AccountsReadiness.Unavailable, (await service.StopAsync(CancellationToken.None)).Readiness);
            authenticated.SetResult(AccountAuthLifecycleResult.Authenticated(A, new Session()));
            Assert.Equal(AccountsReadiness.LateResultRejected, (await starting).Readiness);
            Assert.Equal(0, factory.Calls);
        }

        [Fact]
        public async Task StopDuringBlockedProvisioningFactoryDoesNotWaitForFactoryAndRejectsLateResult()
        {
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            var remote = new Remote(); var leases = new Leases();
            var factory = new ProvisioningFactory(_ =>
            {
                entered.TrySetResult(true);
                if (!release.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Test factory was not released.");
                return remote;
            });
            var service = Service(new Auth(A), factory, new Directory(), leases);
            var starting = Task.Run(async () => await service.StartAsync(App, CancellationToken.None));
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
                Assert.Equal(AccountsReadiness.Unavailable,
                    (await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2))).Readiness);
            }
            finally { release.Set(); }
            Assert.Equal(AccountsReadiness.LateResultRejected, (await starting).Readiness);
            Assert.Equal(0, remote.Calls); Assert.Equal(0, leases.Opened);
        }

        [Fact]
        public async Task ThrowingOrNullProvisioningFactoryKeepsReservationUnavailableAndCanRetry()
        {
            var directory = new Directory(); var remote = new Remote(); var attempts = 0;
            var factory = new ProvisioningFactory(_ =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                if (attempt == 1) throw new InvalidOperationException("provider construction failed");
                if (attempt == 2) return null!;
                return remote;
            });
            var leases = new Leases(); var service = Service(new Auth(A), factory, directory, leases);

            var failed = await service.StartAsync(App, CancellationToken.None);
            Assert.Equal(AccountsReadiness.Unavailable, failed.Readiness); Assert.NotNull(failed.Entry);
            Assert.Equal(0, remote.Calls); Assert.Equal(0, leases.Opened);
            var reservationAfterThrow = await directory.FindAsync(A, App, CancellationToken.None);
            Assert.NotNull(reservationAfterThrow); Assert.Equal(failed.Entry!.InstallationId, reservationAfterThrow!.InstallationId);

            var nullRemote = await service.StartAsync(App, CancellationToken.None);
            Assert.Equal(AccountsReadiness.Unavailable, nullRemote.Readiness); Assert.NotNull(nullRemote.Entry);
            Assert.Equal(failed.Entry.InstallationId, nullRemote.Entry!.InstallationId);
            Assert.Equal(0, remote.Calls); Assert.Equal(0, leases.Opened);

            var retried = await service.StartAsync(App, CancellationToken.None);
            Assert.Equal(AccountsReadiness.Ready, retried.Readiness);
            Assert.Equal(failed.Entry.InstallationId, retried.Entry!.InstallationId);
            Assert.Equal(1, remote.Calls); Assert.Equal(1, leases.Opened); Assert.Equal(3, factory.Calls);
        }

        [Fact]
        public async Task LatePrincipalResultIsRejectedAfterSwitch()
        {
            var delayed = new TaskCompletionSource<AccountAuthLifecycleResult>(TaskCreationOptions.RunContinuationsAsynchronously); var auth = new SequenceAuth(delayed.Task, Task.FromResult(AccountAuthLifecycleResult.Authenticated(B, new Session()))); var service = Service(auth, new Remote(), new Directory(), new Leases());
            var a = service.StartAsync(App, CancellationToken.None); var b = await service.StartAsync(App, CancellationToken.None); delayed.SetResult(AccountAuthLifecycleResult.Authenticated(A, new Session()));
            Assert.Equal(AccountsReadiness.Ready, b.Readiness); Assert.Equal(AccountsReadiness.LateResultRejected, (await a).Readiness); Assert.Equal(B.Subject, service.Snapshot.Entry!.Principal.Subject);
        }

        [Fact]
        public async Task RetirementFailureStopsOldWriterAndQuarantinesReplacement()
        {
            var leases = new Leases(); var service = Service(new SequenceAuth(Task.FromResult(AccountAuthLifecycleResult.Authenticated(A, new Session())), Task.FromResult(AccountAuthLifecycleResult.Authenticated(B, new Session()))), new Remote(), new Directory(), leases);
            Assert.Equal(AccountsReadiness.Ready, (await service.StartAsync(App, CancellationToken.None)).Readiness); Assert.Equal(1, leases.Opened);
            leases.DrainFailures = 1;
            Assert.Equal(AccountsReadiness.RecoveryRequired, (await service.StartAsync(App, CancellationToken.None)).Readiness); Assert.Equal(1, leases.Opened); Assert.Equal(1, leases.Stopped); Assert.Equal(0, leases.Retired);
            Assert.Equal(AccountsReadiness.RecoveryRequired, service.Snapshot.Readiness);
        }

        [Fact]
        public async Task CallerCancellationDuringSwitchCannotKeepOldWriterOrReadyNewScope()
        {
            var drainGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var drainStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var leases = new Leases { FirstDrainGate = drainGate, FirstDrainStarted = drainStarted }; var auth = new SequenceAuth(Task.FromResult(AccountAuthLifecycleResult.Authenticated(A, new Session())), Task.FromResult(AccountAuthLifecycleResult.Authenticated(B, new Session()))); var service = Service(auth, new Remote(), new Directory(), leases);
            Assert.Equal(AccountsReadiness.Ready, (await service.StartAsync(App, CancellationToken.None)).Readiness); using var cancelled = new CancellationTokenSource(); var switching = service.StartAsync(App, cancelled.Token); await drainStarted.Task; cancelled.Cancel(); drainGate.SetResult(true);
            Assert.Equal(AccountsReadiness.Unavailable, (await switching).Readiness); Assert.Equal(1, leases.Stopped); Assert.Equal(1, leases.Retired); Assert.Equal(1, leases.Opened);
        }

        [Fact]
        public async Task SupersededCallerCancellationReturnsCapturedLateOutcome()
        {
            var auth = new FirstCancellationThenAuthenticated(); var service = Service(auth, new Remote(), new Directory(), new Leases()); using var cancelled = new CancellationTokenSource();
            var first = service.StartAsync(App, cancelled.Token); var second = await service.StartAsync(App, CancellationToken.None); cancelled.Cancel(); var old = await first;
            Assert.Equal(AccountsReadiness.Ready, second.Readiness); Assert.Equal(AccountsReadiness.LateResultRejected, old.Readiness); Assert.Equal(1, old.Generation); Assert.Equal(B.Subject, service.Snapshot.Entry!.Principal.Subject);
        }

        [Fact]
        public async Task SupersededCancelledOpenLeaseDrainsBeforeNewerLeaseActivates()
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var leases = new Leases { FirstOpenGate = gate, FirstLeaseOpened = opened }; var auth = new SequenceAuth(Task.FromResult(AccountAuthLifecycleResult.Authenticated(A, new Session())), Task.FromResult(AccountAuthLifecycleResult.Authenticated(B, new Session()))); var service = Service(auth, new Remote(), new Directory(), leases); using var cancelled = new CancellationTokenSource();
            var first = service.StartAsync(App, cancelled.Token); await opened.Task; var second = service.StartAsync(App, CancellationToken.None); cancelled.Cancel(); gate.SetResult(true);
            Assert.Equal(AccountsReadiness.LateResultRejected, (await first).Readiness); Assert.Equal(AccountsReadiness.Ready, (await second).Readiness); Assert.True(leases.EventIndex("retire-1") < leases.EventIndex("open-2"));
        }

        [Fact]
        public async Task StopRetiresReadyScopeEvenWhenCallerIsAlreadyCancelledAndIsIdempotent()
        {
            var leases = new Leases(); var service = Service(new Auth(A), new Remote(), new Directory(), leases);
            Assert.Equal(AccountsReadiness.Ready, (await service.StartAsync(App, CancellationToken.None)).Readiness);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Equal(AccountsReadiness.Unavailable, (await service.StopAsync(cancelled.Token)).Readiness);
            Assert.Equal(1, leases.Stopped); Assert.Equal(1, leases.Retired); Assert.Equal(AccountsReadiness.Unavailable, service.Snapshot.Readiness);
            Assert.Equal(AccountsReadiness.Unavailable, (await service.StopAsync(CancellationToken.None)).Readiness); Assert.Equal(1, leases.Retired);
        }

        [Fact]
        public async Task StopFencesConcurrentStartAndRetirementFailureRequiresRecovery()
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var leases = new Leases { FirstOpenGate = gate, FirstLeaseOpened = opened };
            var service = Service(new Auth(A), new Remote(), new Directory(), leases); var starting = service.StartAsync(App, CancellationToken.None); await opened.Task;
            var stopping = service.StopAsync(CancellationToken.None); gate.SetResult(true);
            Assert.Equal(AccountsReadiness.LateResultRejected, (await starting).Readiness); Assert.Equal(AccountsReadiness.Unavailable, (await stopping).Readiness); Assert.Equal(1, leases.Retired);
            var failing = Service(new Auth(A), new Remote(), new Directory(), new Leases { DrainFailures = 1 }); Assert.Equal(AccountsReadiness.Ready, (await failing.StartAsync(App, CancellationToken.None)).Readiness); Assert.Equal(AccountsReadiness.RecoveryRequired, (await failing.StopAsync(CancellationToken.None)).Readiness);
        }

        [Fact]
        public async Task StopObservesLateLeaseRetirementFailureAndRepeatedStopDoesNotHideIt()
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var leases = new Leases { FirstOpenGate = gate, FirstLeaseOpened = opened };
            var service = Service(new Auth(A), new Remote(), new Directory(), leases); var starting = service.StartAsync(App, CancellationToken.None); await opened.Task;
            var stopping = service.StopAsync(CancellationToken.None); leases.DrainFailures = 1; gate.SetResult(true);
            Assert.Equal(AccountsReadiness.LateResultRejected, (await starting).Readiness); Assert.Equal(AccountsReadiness.RecoveryRequired, (await stopping).Readiness); Assert.Equal(AccountsReadiness.RecoveryRequired, service.Snapshot.Readiness);
            Assert.Equal(AccountsReadiness.RecoveryRequired, (await service.StopAsync(CancellationToken.None)).Readiness); Assert.Equal(1, leases.Stopped);
        }

        [Fact]
        public async Task StopBoundsAConcurrentNoncooperativeOpenAndPublishesRecovery()
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var leases = new Leases { FirstOpenGate = gate, FirstLeaseOpened = opened };
            var service = new AccountsLifecycleService(new Auth(A), new ProvisioningFactory(_ => new Remote()), new Directory(), leases, new UuidV7Generator(new Clock(), new Random()), TimeSpan.FromMilliseconds(25));
            var starting = service.StartAsync(App, CancellationToken.None); await opened.Task;
            var stopped = await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2)); Assert.Equal(AccountsReadiness.RecoveryRequired, stopped.Readiness); Assert.Equal(AccountsReadiness.RecoveryRequired, service.Snapshot.Readiness);
            gate.SetResult(true); Assert.Equal(AccountsReadiness.LateResultRejected, (await starting).Readiness);
        }

        [Fact]
        public async Task SqliteDirectoryReopensReservationBeforeIssuedAccountBinding()
        {
            var folder = Path.Combine(Path.GetTempPath(), "game-platform-cl101", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(folder); var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new StorageScope("test-backend", new PlatformId(App.ToString()), new PlatformId("principal-directory")); var migrations = SqlitePlatformMigrationRegistry.Migrations;
            try
            {
                var db = await SqliteDatabase.OpenAsync(path, scope, migrations, CancellationToken.None); var store = new SqliteAccountsDirectoryStore(db, scope); var generator = new UuidV7Generator(new Clock(), new Random()); var installation = generator.NewId(); var stream = new ClientStreamId(generator.NewId()); var reserved = await store.ReserveAsync(A, App, installation, stream, CancellationToken.None); Assert.False(reserved.HasIssuedAccount); Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
                db = await SqliteDatabase.OpenAsync(path, scope, migrations, CancellationToken.None); store = new SqliteAccountsDirectoryStore(db, scope); var reopened = await store.FindAsync(A, App, CancellationToken.None); Assert.NotNull(reopened); Assert.Equal(installation, reopened!.InstallationId); var bound = await store.BindIssuedAccountAsync(reopened, new PlatformUserId(Guid.Parse("0199f9a0-2222-7777-8888-999999999999")), "active", CancellationToken.None); Assert.True(bound.HasIssuedAccount); Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
            finally { try { if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); } catch (IOException) { } }
        }

        private static AccountsLifecycleService Service(IAccountsAuthLifecycle auth, Remote remote, Directory directory, Leases leases) => Service(auth, new ProvisioningFactory(_ => remote), directory, leases);
        private static AccountsLifecycleService Service(IAccountsAuthLifecycle auth, ProvisioningFactory factory, Directory directory, Leases leases) => new AccountsLifecycleService(auth, factory, directory, leases, new UuidV7Generator(new Clock(), new Random()));
        private sealed class Auth : IAccountsAuthLifecycle { private readonly AccountAuthLifecycleResult result; public Auth(AccountPrincipalDescriptor principal) : this(AccountAuthLifecycleResult.Authenticated(principal, new Session())) { } public Auth(AccountAuthLifecycleResult result) { this.result = result; } public Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken token) => Task.FromResult(result); }
        private sealed class PendingAuth : IAccountsAuthLifecycle { private readonly Task<AccountAuthLifecycleResult> result; public PendingAuth(Task<AccountAuthLifecycleResult> result) { this.result = result; } public Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken token) => result; }
        private sealed class SequenceAuth : IAccountsAuthLifecycle { private readonly Queue<Task<AccountAuthLifecycleResult>> values; public SequenceAuth(params Task<AccountAuthLifecycleResult>[] values) { this.values = new Queue<Task<AccountAuthLifecycleResult>>(values); } public Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken token) => values.Dequeue(); }
        private sealed class FirstCancellationThenAuthenticated : IAccountsAuthLifecycle { private int calls; public async Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken token) { if (Interlocked.Increment(ref calls) == 1) { await Task.Delay(Timeout.Infinite, token); } return AccountAuthLifecycleResult.Authenticated(B, new Session()); } }
        private sealed class Session : IAuthSession { public string SessionKey => "test-session"; public Task<AccessTokenSnapshot> GetAsync(CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("opaque", 0)); public Task<AccessTokenSnapshot> RefreshAsync(long generation, CancellationToken token) => GetAsync(token); }
        private sealed class ProvisioningFactory : IProvisioningRemoteFactory { private readonly Func<IAuthSession, IProvisioningRemote> create; public ProvisioningFactory(Func<IAuthSession, IProvisioningRemote> create) { this.create = create; } public int Calls; public IAuthSession? LastSession; public IProvisioningRemote Create(IAuthSession authenticatedSession) { Calls++; LastSession = authenticatedSession; return create(authenticatedSession); } }
        private sealed class Remote : IProvisioningRemote { public int Calls; public bool FailFirst; public Task<RemoteResult<ProvisioningSnapshot>> ProvisionAsync(AppId app, Guid installation, ClientStreamId stream, CancellationToken token) { Calls++; if (FailFirst && Calls == 1) return Task.FromResult(RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain))); return Task.FromResult(RemoteResult<ProvisioningSnapshot>.Success(new ProvisioningSnapshot(new PlatformUserId(Guid.Parse("0199f9a0-2222-7777-8888-999999999999")), app, "active", stream, 1, 1))); } }
        private sealed class Directory : IAccountDirectoryStore { private readonly Dictionary<string, AccountDirectoryEntry> values = new Dictionary<string, AccountDirectoryEntry>(); private static string Key(AccountPrincipalDescriptor p, AppId a) => p.BackendNamespace + ":" + a + ":" + p.Issuer + ":" + p.Subject; public Task<AccountDirectoryEntry?> FindAsync(AccountPrincipalDescriptor p, AppId a, CancellationToken t) => Task.FromResult(values.TryGetValue(Key(p, a), out var value) ? value : null); public Task<AccountDirectoryEntry> ReserveAsync(AccountPrincipalDescriptor p, AppId a, Guid i, ClientStreamId s, CancellationToken t) { var key = Key(p, a); if (!values.TryGetValue(key, out var value)) values[key] = value = new AccountDirectoryEntry(p, a, i, s, null, null); return Task.FromResult(value); } public Task<AccountDirectoryEntry> BindIssuedAccountAsync(AccountDirectoryEntry r, PlatformUserId id, string membership, CancellationToken t) { var value = new AccountDirectoryEntry(r.Principal, r.AppId, r.InstallationId, r.StreamId, id, membership); values[Key(r.Principal, r.AppId)] = value; return Task.FromResult(value); } }
        private sealed class Leases : IAccountScopeLeaseFactory { private readonly List<string> events = new List<string>(); public bool Ready = true; public AccountBootstrapResult Bootstrap = AccountBootstrapResult.Complete; public int DrainFailures; public int Opened; public int Retired; public int Stopped; public IAuthSession? LastAuthenticatedSession; public TaskCompletionSource<bool>? FirstOpenGate; public TaskCompletionSource<bool>? FirstLeaseOpened; public TaskCompletionSource<bool>? FirstDrainGate; public TaskCompletionSource<bool>? FirstDrainStarted; public async Task<IAccountScopeLease> OpenAuthenticatedAsync(AccountDirectoryEntry e, IAuthSession s, long g, CancellationToken t) { LastAuthenticatedSession = s; var lease = new Lease(e, g, Ready, Bootstrap, this); if (lease.Ordinal == 1 && FirstOpenGate != null) { FirstLeaseOpened!.SetResult(true); await FirstOpenGate.Task.ConfigureAwait(false); } return lease; } public Task<IAccountScopeLease> ReopenOfflineAsync(AccountDirectoryEntry e, long g, CancellationToken t) => Task.FromResult<IAccountScopeLease>(new Lease(e, g, Ready, Bootstrap, this)); public int EventIndex(string value) { lock (events) return events.IndexOf(value); } public void OpenedEvent(int ordinal) { lock (events) events.Add("open-" + ordinal); } public void RetiredEvent(int ordinal) { lock (events) events.Add("retire-" + ordinal); } }
        private sealed class Lease : IAccountScopeLease { private readonly bool ready; private readonly AccountBootstrapResult bootstrap; private readonly Leases owner; public Lease(AccountDirectoryEntry e, long g, bool ready, AccountBootstrapResult bootstrap, Leases owner) { Entry = e; Generation = g; this.ready = ready; this.bootstrap = bootstrap; this.owner = owner; Ordinal = Interlocked.Increment(ref owner.Opened); owner.OpenedEvent(Ordinal); } public int Ordinal { get; } public AccountDirectoryEntry Entry { get; } public long Generation { get; } public Task<bool> IsBootstrapReadyAsync(CancellationToken t) => Task.FromResult(ready); public Task<AccountBootstrapResult> BootstrapAsync(CancellationToken t) => Task.FromResult(bootstrap); public Task StopAdmissionsAsync(CancellationToken t) { Interlocked.Increment(ref owner.Stopped); return Task.CompletedTask; } public async Task DrainAndRetireAsync(CancellationToken t) { if (Ordinal == 1 && owner.FirstDrainGate != null) { owner.FirstDrainStarted!.SetResult(true); await owner.FirstDrainGate.Task.ConfigureAwait(false); } if (Interlocked.CompareExchange(ref owner.DrainFailures, 0, 0) > 0) { Interlocked.Decrement(ref owner.DrainFailures); throw new InvalidOperationException("drain failure"); } Interlocked.Increment(ref owner.Retired); owner.RetiredEvent(Ordinal); } }
        private sealed class Clock : IUnixMillisecondClock { public long GetUnixMilliseconds() => 1; }
        private sealed class Random : IUuidRandomSource { public void Fill(Span<byte> destination) { for (var i = 0; i < destination.Length; i++) destination[i] = (byte)(i + 1); } }
    }
}
