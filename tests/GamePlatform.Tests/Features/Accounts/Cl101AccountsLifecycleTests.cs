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
        public async Task LatePrincipalResultIsRejectedAfterSwitch()
        {
            var delayed = new TaskCompletionSource<AccountAuthLifecycleResult>(TaskCreationOptions.RunContinuationsAsynchronously); var auth = new SequenceAuth(delayed.Task, Task.FromResult(AccountAuthLifecycleResult.Authenticated(B, new Session()))); var service = Service(auth, new Remote(), new Directory(), new Leases());
            var a = service.StartAsync(App, CancellationToken.None); var b = await service.StartAsync(App, CancellationToken.None); delayed.SetResult(AccountAuthLifecycleResult.Authenticated(A, new Session()));
            Assert.Equal(AccountsReadiness.Ready, b.Readiness); Assert.Equal(AccountsReadiness.LateResultRejected, (await a).Readiness); Assert.Equal(B.Subject, service.Snapshot.Entry!.Principal.Subject);
        }

        [Fact]
        public async Task SqliteDirectoryReopensReservationBeforeIssuedAccountBinding()
        {
            var folder = Path.Combine(Path.GetTempPath(), "game-platform-cl101", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(folder); var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new StorageScope("test-backend", new PlatformId(App.ToString()), new PlatformId("principal-directory")); var migrations = SqlitePlatformMigrationRegistry.Migrations.Concat(new[] { SqliteAccountsDirectoryMigration.Create(4) }).ToArray();
            try
            {
                var db = await SqliteDatabase.OpenAsync(path, scope, migrations, CancellationToken.None); var store = new SqliteAccountsDirectoryStore(db, scope); var generator = new UuidV7Generator(new Clock(), new Random()); var installation = generator.NewId(); var stream = new ClientStreamId(generator.NewId()); var reserved = await store.ReserveAsync(A, App, installation, stream, CancellationToken.None); Assert.False(reserved.HasIssuedAccount); Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
                db = await SqliteDatabase.OpenAsync(path, scope, migrations, CancellationToken.None); store = new SqliteAccountsDirectoryStore(db, scope); var reopened = await store.FindAsync(A, App, CancellationToken.None); Assert.NotNull(reopened); Assert.Equal(installation, reopened!.InstallationId); var bound = await store.BindIssuedAccountAsync(reopened, new PlatformUserId(Guid.Parse("0199f9a0-2222-7777-8888-999999999999")), "active", CancellationToken.None); Assert.True(bound.HasIssuedAccount); Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
            finally { try { if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); } catch (IOException) { } }
        }

        private static AccountsLifecycleService Service(IAccountsAuthLifecycle auth, Remote remote, Directory directory, Leases leases) => new AccountsLifecycleService(auth, remote, directory, leases, new UuidV7Generator(new Clock(), new Random()));
        private sealed class Auth : IAccountsAuthLifecycle { private readonly AccountAuthLifecycleResult result; public Auth(AccountPrincipalDescriptor principal) : this(AccountAuthLifecycleResult.Authenticated(principal, new Session())) { } public Auth(AccountAuthLifecycleResult result) { this.result = result; } public Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken token) => Task.FromResult(result); }
        private sealed class SequenceAuth : IAccountsAuthLifecycle { private readonly Queue<Task<AccountAuthLifecycleResult>> values; public SequenceAuth(params Task<AccountAuthLifecycleResult>[] values) { this.values = new Queue<Task<AccountAuthLifecycleResult>>(values); } public Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken token) => values.Dequeue(); }
        private sealed class Session : IAuthSession { public string SessionKey => "test-session"; public Task<AccessTokenSnapshot> GetAsync(CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("opaque", 0)); public Task<AccessTokenSnapshot> RefreshAsync(long generation, CancellationToken token) => GetAsync(token); }
        private sealed class Remote : IProvisioningRemote { public int Calls; public bool FailFirst; public Task<RemoteResult<ProvisioningSnapshot>> ProvisionAsync(AppId app, Guid installation, ClientStreamId stream, CancellationToken token) { Calls++; if (FailFirst && Calls == 1) return Task.FromResult(RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain))); return Task.FromResult(RemoteResult<ProvisioningSnapshot>.Success(new ProvisioningSnapshot(new PlatformUserId(Guid.Parse("0199f9a0-2222-7777-8888-999999999999")), app, "active", stream, 1, 1))); } }
        private sealed class Directory : IAccountDirectoryStore { private readonly Dictionary<string, AccountDirectoryEntry> values = new Dictionary<string, AccountDirectoryEntry>(); private static string Key(AccountPrincipalDescriptor p, AppId a) => p.BackendNamespace + ":" + a + ":" + p.Issuer + ":" + p.Subject; public Task<AccountDirectoryEntry?> FindAsync(AccountPrincipalDescriptor p, AppId a, CancellationToken t) => Task.FromResult(values.TryGetValue(Key(p,a), out var value) ? value : null); public Task<AccountDirectoryEntry> ReserveAsync(AccountPrincipalDescriptor p, AppId a, Guid i, ClientStreamId s, CancellationToken t) { var key=Key(p,a); if(!values.TryGetValue(key,out var value)) values[key]=value=new AccountDirectoryEntry(p,a,i,s,null,null); return Task.FromResult(value); } public Task<AccountDirectoryEntry> BindIssuedAccountAsync(AccountDirectoryEntry r, PlatformUserId id, string membership, CancellationToken t) { var value=new AccountDirectoryEntry(r.Principal,r.AppId,r.InstallationId,r.StreamId,id,membership); values[Key(r.Principal,r.AppId)]=value; return Task.FromResult(value); } }
        private sealed class Leases : IAccountScopeLeaseFactory { public bool Ready=true; public AccountBootstrapResult Bootstrap=AccountBootstrapResult.Complete; public Task<IAccountScopeLease> OpenAuthenticatedAsync(AccountDirectoryEntry e,IAuthSession s,long g,CancellationToken t)=>Task.FromResult<IAccountScopeLease>(new Lease(e,g,Ready,Bootstrap)); public Task<IAccountScopeLease> ReopenOfflineAsync(AccountDirectoryEntry e,long g,CancellationToken t)=>Task.FromResult<IAccountScopeLease>(new Lease(e,g,Ready,Bootstrap)); }
        private sealed class Lease : IAccountScopeLease { private readonly bool ready; private readonly AccountBootstrapResult bootstrap; public Lease(AccountDirectoryEntry e,long g,bool ready,AccountBootstrapResult bootstrap){Entry=e;Generation=g;this.ready=ready;this.bootstrap=bootstrap;} public AccountDirectoryEntry Entry{get;}public long Generation{get;}public Task<bool> IsBootstrapReadyAsync(CancellationToken t)=>Task.FromResult(ready);public Task<AccountBootstrapResult> BootstrapAsync(CancellationToken t)=>Task.FromResult(bootstrap);public Task StopAdmissionsAsync(CancellationToken t)=>Task.CompletedTask;public Task DrainAndRetireAsync(CancellationToken t)=>Task.CompletedTask; }
        private sealed class Clock:IUnixMillisecondClock { public long GetUnixMilliseconds()=>1; } private sealed class Random:IUuidRandomSource { public void Fill(Span<byte> destination){for(var i=0;i<destination.Length;i++)destination[i]=(byte)(i+1);} }
    }
}
