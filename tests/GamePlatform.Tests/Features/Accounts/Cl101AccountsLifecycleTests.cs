#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Accounts;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Sqlite.Features.Accounts;
using SQLite;

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

        [Theory]
        [InlineData(AccountBootstrapResult.Complete, AccountsReadiness.Ready)]
        [InlineData(AccountBootstrapResult.Incomplete, AccountsReadiness.Bootstrapping)]
        public async Task RecoverAsyncReachesReadyOnlyThroughProvisionAndCompleteBootstrap(AccountBootstrapResult bootstrap, AccountsReadiness expected)
        {
            var remote = new Remote(); var directory = new Directory(); var leases = new Leases { Bootstrap = bootstrap };
            var recovery = new Recovery(AccountAuthLifecycleResult.Authenticated(A, new Session()));
            var service = Service(new Auth(AccountAuthLifecycleResult.RecoveryRequired(A)), remote, directory, leases, recovery);
            var started = await service.StartAsync(App, CancellationToken.None);
            Assert.Equal(AccountsReadiness.RecoveryRequired, started.Readiness); Assert.Equal(0, remote.Calls); Assert.Equal(0, recovery.Calls);

            var recovered = await service.RecoverAsync(App, CancellationToken.None);

            Assert.Equal(expected, recovered.Readiness); Assert.Equal(started.Generation + 1, recovered.Generation);
            Assert.Equal(1, recovery.Calls); Assert.Equal(1, remote.Calls); Assert.Equal(1, leases.Opened);
            Assert.True((await directory.FindAsync(A, App, CancellationToken.None))!.HasIssuedAccount);
            Assert.Equal(expected, service.Snapshot.Readiness);
        }

        [Theory]
        [InlineData(AccountAuthLifecycleKind.RecoveryRequired, AccountsReadiness.RecoveryRequired)]
        [InlineData(AccountAuthLifecycleKind.Cancelled, AccountsReadiness.Cancelled)]
        [InlineData(AccountAuthLifecycleKind.UnavailableOffline, AccountsReadiness.UnavailableOffline)]
        public async Task UnsuccessfulRecoverAsyncNeverProvisions(AccountAuthLifecycleKind kind, AccountsReadiness expected)
        {
            var outcome = kind == AccountAuthLifecycleKind.RecoveryRequired ? AccountAuthLifecycleResult.RecoveryRequired(A) : kind == AccountAuthLifecycleKind.Cancelled ? AccountAuthLifecycleResult.Cancelled() : AccountAuthLifecycleResult.UnavailableOffline(null);
            var remote = new Remote(); var leases = new Leases(); var recovery = new Recovery(outcome);
            var service = Service(new Auth(AccountAuthLifecycleResult.RecoveryRequired(A)), remote, new Directory(), leases, recovery);
            await service.StartAsync(App, CancellationToken.None);

            var recovered = await service.RecoverAsync(App, CancellationToken.None);

            Assert.Equal(expected, recovered.Readiness); Assert.Equal(2, recovered.Generation); Assert.Equal(1, recovery.Calls);
            Assert.Equal(0, remote.Calls); Assert.Equal(0, leases.Opened);
        }

        [Fact]
        public async Task OfflineRecoveryResultNeverReopensAnIssuedAccountScope()
        {
            var directory = new Directory();
            var reserved = await directory.ReserveAsync(A, App, Guid.Parse("0199f9a0-3333-7777-8888-999999999999"), new ClientStreamId(Guid.Parse("0199f9a0-4444-7777-8888-999999999999")), CancellationToken.None);
            await directory.BindIssuedAccountAsync(reserved, new PlatformUserId(Guid.Parse("0199f9a0-2222-7777-8888-999999999999")), "active", CancellationToken.None);
            var leases = new Leases(); var recovery = new Recovery(AccountAuthLifecycleResult.UnavailableOffline(A));
            var service = Service(new Auth(AccountAuthLifecycleResult.RecoveryRequired(A)), new Remote(), directory, leases, recovery);
            await service.StartAsync(App, CancellationToken.None);

            Assert.Equal(AccountsReadiness.UnavailableOffline, (await service.RecoverAsync(App, CancellationToken.None)).Readiness);
            Assert.Equal(1, recovery.Calls); Assert.Equal(0, leases.Opened);

            var activeLeases = new Leases();
            var withActive = Service(new SequenceAuth(Task.FromResult(AccountAuthLifecycleResult.Authenticated(A, new Session())), Task.FromResult(AccountAuthLifecycleResult.RecoveryRequired(A))), new Remote(), new Directory(), activeLeases, new Recovery(AccountAuthLifecycleResult.UnavailableOffline(A)));
            Assert.Equal(AccountsReadiness.Ready, (await withActive.StartAsync(App, CancellationToken.None)).Readiness);
            Assert.Equal(AccountsReadiness.RecoveryRequired, (await withActive.StartAsync(App, CancellationToken.None)).Readiness);
            Assert.Equal(AccountsReadiness.UnavailableOffline, (await withActive.RecoverAsync(App, CancellationToken.None)).Readiness);
            Assert.Equal(1, activeLeases.Opened); Assert.Equal(0, activeLeases.Stopped); Assert.Equal(0, activeLeases.Retired);
            Assert.Equal(AccountsReadiness.Unavailable, (await withActive.StopAsync(CancellationToken.None)).Readiness);
            Assert.Equal(1, activeLeases.Retired);
        }

        [Fact]
        public async Task RecoverAsyncIsAdmittedFromUnavailableButNotAfterStop()
        {
            var remote = new Remote { FailFirst = true }; var recovery = new Recovery(AccountAuthLifecycleResult.Authenticated(A, new Session()));
            var service = Service(new Auth(A), remote, new Directory(), new Leases(), recovery);
            Assert.Equal(AccountsReadiness.Unavailable, (await service.StartAsync(App, CancellationToken.None)).Readiness);

            Assert.Equal(AccountsReadiness.Ready, (await service.RecoverAsync(App, CancellationToken.None)).Readiness);
            Assert.Equal(1, recovery.Calls); Assert.Equal(2, remote.Calls);

            var stoppedRecovery = new Recovery(AccountAuthLifecycleResult.Authenticated(A, new Session()));
            var stopped = Service(new Auth(AccountAuthLifecycleResult.RecoveryRequired(A)), new Remote(), new Directory(), new Leases(), stoppedRecovery);
            await stopped.StartAsync(App, CancellationToken.None);
            Assert.Equal(AccountsReadiness.Unavailable, (await stopped.StopAsync(CancellationToken.None)).Readiness);
            Assert.Equal(AccountsReadiness.Unavailable, (await stopped.RecoverAsync(App, CancellationToken.None)).Readiness);
            Assert.Equal(0, stoppedRecovery.Calls);
        }

        [Fact]
        public async Task RecoverAsyncIsIgnoredWhenReadyAndRequiresAConfiguredPort()
        {
            var leases = new Leases(); var recovery = new Recovery(AccountAuthLifecycleResult.Authenticated(A, new Session()));
            var service = Service(new Auth(A), new Remote(), new Directory(), leases, recovery);
            var ready = await service.StartAsync(App, CancellationToken.None);

            var ignored = await service.RecoverAsync(App, CancellationToken.None);

            Assert.Equal(AccountsReadiness.Ready, ignored.Readiness); Assert.Equal(ready.Generation, ignored.Generation);
            Assert.Equal(0, recovery.Calls); Assert.Equal(1, leases.Opened);
            var unconfigured = Service(new Auth(AccountAuthLifecycleResult.RecoveryRequired(A)), new Remote(), new Directory(), new Leases());
            await unconfigured.StartAsync(App, CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => unconfigured.RecoverAsync(App, CancellationToken.None));
        }

        [Fact]
        public async Task LateRecoveryResultIsRejectedAfterStopOrNewerStart()
        {
            var delayed = new TaskCompletionSource<AccountAuthLifecycleResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var remote = new Remote(); var service = Service(new Auth(AccountAuthLifecycleResult.RecoveryRequired(A)), remote, new Directory(), new Leases(), new Recovery(delayed.Task));
            await service.StartAsync(App, CancellationToken.None);
            var recovering = service.RecoverAsync(App, CancellationToken.None);
            Assert.Equal(AccountsReadiness.Unavailable, (await service.StopAsync(CancellationToken.None)).Readiness);
            delayed.SetResult(AccountAuthLifecycleResult.Authenticated(A, new Session()));
            Assert.Equal(AccountsReadiness.LateResultRejected, (await recovering).Readiness); Assert.Equal(0, remote.Calls);
            Assert.Equal(AccountsReadiness.Unavailable, (await service.RecoverAsync(App, CancellationToken.None)).Readiness);

            var superseded = new TaskCompletionSource<AccountAuthLifecycleResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var auth = new SequenceAuth(Task.FromResult(AccountAuthLifecycleResult.RecoveryRequired(A)), Task.FromResult(AccountAuthLifecycleResult.Authenticated(B, new Session())));
            var switching = Service(auth, new Remote(), new Directory(), new Leases(), new Recovery(superseded.Task));
            await switching.StartAsync(App, CancellationToken.None);
            var stale = switching.RecoverAsync(App, CancellationToken.None);
            Assert.Equal(AccountsReadiness.Ready, (await switching.StartAsync(App, CancellationToken.None)).Readiness);
            superseded.SetResult(AccountAuthLifecycleResult.Authenticated(A, new Session()));
            Assert.Equal(AccountsReadiness.LateResultRejected, (await stale).Readiness); Assert.Equal(B.Subject, switching.Snapshot.Entry!.Principal.Subject);
        }

        [Fact]
        public async Task RecoverAsyncDoesNotHideFailedScopeRetirement()
        {
            var leases = new Leases(); var recovery = new Recovery(AccountAuthLifecycleResult.Authenticated(B, new Session()));
            var auth = new SequenceAuth(Task.FromResult(AccountAuthLifecycleResult.Authenticated(A, new Session())), Task.FromResult(AccountAuthLifecycleResult.Authenticated(B, new Session())));
            var service = Service(auth, new Remote(), new Directory(), leases, recovery);
            Assert.Equal(AccountsReadiness.Ready, (await service.StartAsync(App, CancellationToken.None)).Readiness);
            leases.DrainFailures = 1;
            var quarantined = await service.StartAsync(App, CancellationToken.None);
            Assert.Equal(AccountsReadiness.RecoveryRequired, quarantined.Readiness);

            var recovered = await service.RecoverAsync(App, CancellationToken.None);

            Assert.Equal(AccountsReadiness.RecoveryRequired, recovered.Readiness); Assert.Equal(quarantined.Generation, recovered.Generation);
            Assert.Equal(0, recovery.Calls); Assert.Equal(1, leases.Opened); Assert.Equal(0, leases.Retired);
        }

        [Fact]
        public async Task SqliteDirectoryReopensReservationBeforeIssuedAccountBinding()
        {
            var folder = Path.Combine(Path.GetTempPath(), "game-platform-cl101", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(folder); var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new AccountDirectoryScope(new BackendNamespace("test-backend"), App);
            try
            {
                var db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None); var store = new SqliteAccountsDirectoryStore(db, scope); var generator = new UuidV7Generator(new Clock(), new Random()); var installation = generator.NewId(); var stream = new ClientStreamId(generator.NewId()); var reserved = await store.ReserveAsync(A, App, installation, stream, CancellationToken.None); Assert.False(reserved.HasIssuedAccount); Assert.True(await store.HasAnyEntryAsync(CancellationToken.None)); Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
                db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None); store = new SqliteAccountsDirectoryStore(db, scope); var reopened = await store.FindAsync(A, App, CancellationToken.None); Assert.NotNull(reopened); Assert.Equal(installation, reopened!.InstallationId); Assert.Equal(stream, reopened.StreamId); var issuedId = new PlatformUserId(Guid.Parse("0199f9a0-2222-7777-8888-999999999999")); var bound = await store.BindIssuedAccountAsync(reopened, issuedId, "active", CancellationToken.None); Assert.True(bound.HasIssuedAccount); Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
                db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None); store = new SqliteAccountsDirectoryStore(db, scope); var issued = await store.FindAsync(A, App, CancellationToken.None); Assert.NotNull(issued); Assert.Equal(installation, issued!.InstallationId); Assert.Equal(issuedId, issued.AccountId); Assert.Equal("active", issued.MembershipStatus); Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
            finally { try { if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); } catch (IOException) { } }
        }

        [Fact]
        public async Task SqlitePrincipalDirectoryStartsEmptyAndRejectsBackendOrAppScopeEscapes()
        {
            var folder = Path.Combine(Path.GetTempPath(), "game-platform-cl101", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(folder); var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new AccountDirectoryScope(new BackendNamespace("test-backend"), App);
            try
            {
                var db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None); var store = new SqliteAccountsDirectoryStore(db, scope);
                Assert.False(await store.HasAnyEntryAsync(CancellationToken.None));
                Assert.Null(await store.FindAsync(A, App, CancellationToken.None));

                var otherBackend = new AccountPrincipalDescriptor(new BackendNamespace("other-backend"), A.Issuer, A.Subject);
                var otherApp = new AppId(Guid.Parse("0199f9a1-1111-7777-8888-999999999999"));
                var installation = Guid.Parse("0199f9a1-2222-7777-8888-999999999999"); var stream = new ClientStreamId(Guid.Parse("0199f9a1-3333-7777-8888-999999999999"));
                await Assert.ThrowsAsync<StorageException>(async () => await store.FindAsync(otherBackend, App, CancellationToken.None));
                await Assert.ThrowsAsync<StorageException>(async () => await store.FindAsync(A, otherApp, CancellationToken.None));
                await Assert.ThrowsAsync<StorageException>(async () => await store.ReserveAsync(otherBackend, App, installation, stream, CancellationToken.None));
                await Assert.ThrowsAsync<StorageException>(async () => await store.ReserveAsync(A, otherApp, installation, stream, CancellationToken.None));
                var foreignBackendReservation = new AccountDirectoryEntry(otherBackend, App, installation, stream, null, null);
                var foreignAppReservation = new AccountDirectoryEntry(A, otherApp, installation, stream, null, null);
                var issued = new PlatformUserId(Guid.Parse("0199f9a1-4444-7777-8888-999999999999"));
                await Assert.ThrowsAsync<StorageException>(async () => await store.BindIssuedAccountAsync(foreignBackendReservation, issued, "active", CancellationToken.None));
                await Assert.ThrowsAsync<StorageException>(async () => await store.BindIssuedAccountAsync(foreignAppReservation, issued, "active", CancellationToken.None));
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
                await Assert.ThrowsAsync<StorageException>(() => SqliteAccountsDirectoryDatabase.OpenAsync(path,
                    new AccountDirectoryScope(new BackendNamespace("other-backend"), App), CancellationToken.None));
                await Assert.ThrowsAsync<StorageException>(() => SqliteAccountsDirectoryDatabase.OpenAsync(path,
                    new AccountDirectoryScope(new BackendNamespace("test-backend"), otherApp), CancellationToken.None));
            }
            finally { try { if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); } catch (IOException) { } }
        }

        [Fact]
        public async Task SqlitePrincipalDirectoryFailsClosedOnCorruptDatabase()
        {
            var folder = Path.Combine(Path.GetTempPath(), "game-platform-cl101", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(folder); var path = Path.Combine(folder, "directory.sqlite3");
            try
            {
                await File.WriteAllBytesAsync(path, new byte[] { 0x13, 0x37, 0x00, 0x01, 0x02, 0x03 });
                var failure = await Assert.ThrowsAsync<StorageException>(() => SqliteAccountsDirectoryDatabase.OpenAsync(path,
                    new AccountDirectoryScope(new BackendNamespace("test-backend"), App), CancellationToken.None));
                Assert.Equal(StorageFailure.Corrupt, failure.Failure);
            }
            finally { try { if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); } catch (IOException) { } }
        }

        [Fact]
        public async Task SqlitePrincipalDirectoryHasAnyEntryFailsClosedWhenForeignScopeRowExists()
        {
            var folder = Path.Combine(Path.GetTempPath(), "game-platform-cl101", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(folder); var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new AccountDirectoryScope(new BackendNamespace("test-backend"), App);
            try
            {
                var db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None);
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
                using (var raw = new SQLiteConnection(path))
                {
                    raw.Execute("INSERT INTO gp_account_principal_directory(backend_namespace,app_id,issuer,subject,installation_id,client_stream_id,platform_user_id,membership_status) VALUES (?,?,?,?,?,?,NULL,NULL)",
                        "other-backend", App.ToString(), "https://issuer.test", "foreign-backend", "0199f9a1-1111-7777-8888-999999999999", "0199f9a1-2222-7777-8888-999999999999");
                    raw.Execute("INSERT INTO gp_account_principal_directory(backend_namespace,app_id,issuer,subject,installation_id,client_stream_id,platform_user_id,membership_status) VALUES (?,?,?,?,?,?,NULL,NULL)",
                        scope.BackendNamespace.Value, "0199f9a1-3333-7777-8888-999999999999", "https://issuer.test", "foreign-app", "0199f9a1-4444-7777-8888-999999999999", "0199f9a1-5555-7777-8888-999999999999");
                }

                db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None);
                var store = new SqliteAccountsDirectoryStore(db, scope);
                var error = await Assert.ThrowsAsync<StorageException>(() => store.HasAnyEntryAsync(CancellationToken.None));
                Assert.Equal(StorageFailure.InvalidOwner, error.Failure);
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
            }
            finally { try { if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); } catch (IOException) { } }
        }

        [Theory]
        [InlineData("version-zero")]
        [InlineData("extra-version")]
        [InlineData("version-gap")]
        public async Task SqlitePrincipalDirectoryRejectsNonExactMigrationJournal(string corruption)
        {
            var folder = Path.Combine(Path.GetTempPath(), "game-platform-cl101", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(folder); var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new AccountDirectoryScope(new BackendNamespace("test-backend"), App);
            try
            {
                var db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None);
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
                using (var raw = new SQLiteConnection(path))
                {
                    if (corruption == "version-zero")
                        raw.Execute("INSERT INTO gp_principal_directory_schema_migrations(version,migration_id,checksum,backend_namespace,app_id,applied_at) VALUES (0,'review-version-zero','invalid',?,?,1)", scope.BackendNamespace.Value, App.ToString());
                    else if (corruption == "extra-version")
                        raw.Execute("INSERT INTO gp_principal_directory_schema_migrations(version,migration_id,checksum,backend_namespace,app_id,applied_at) VALUES (2,'review-extra-version','invalid',?,?,1)", scope.BackendNamespace.Value, App.ToString());
                    else
                        raw.Execute("UPDATE gp_principal_directory_schema_migrations SET version=2 WHERE version=1");
                }

                var error = await Assert.ThrowsAsync<StorageException>(() => SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None));
                Assert.Equal(StorageFailure.Migration, error.Failure);
            }
            finally { try { if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); } catch (IOException) { } }
        }

        [Theory]
        [InlineData("data-table")]
        [InlineData("issued-account-index")]
        [InlineData("malformed-issued-account-index")]
        public async Task SqlitePrincipalDirectoryRejectsMissingOrMalformedRequiredDataSchema(string objectToRemove)
        {
            var folder = Path.Combine(Path.GetTempPath(), "game-platform-cl101", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(folder); var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new AccountDirectoryScope(new BackendNamespace("test-backend"), App);
            try
            {
                var db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None);
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
                using (var raw = new SQLiteConnection(path))
                {
                    if (objectToRemove == "data-table") raw.Execute("DROP TABLE gp_account_principal_directory");
                    else if (objectToRemove == "issued-account-index") raw.Execute("DROP INDEX gp_account_principal_directory_issued_account");
                    else
                    {
                        raw.Execute("DROP INDEX gp_account_principal_directory_issued_account");
                        raw.Execute("CREATE UNIQUE INDEX gp_account_principal_directory_issued_account ON gp_account_principal_directory(app_id) WHERE platform_user_id IS NOT NULL");
                    }
                }

                var error = await Assert.ThrowsAsync<StorageException>(() => SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None));
                Assert.Equal(StorageFailure.Migration, error.Failure);
            }
            finally { try { if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); } catch (IOException) { } }
        }

        [Theory]
        [InlineData("installation-unique")]
        [InlineData("stream-unique")]
        [InlineData("issued-membership-check")]
        [InlineData("issued-index-extra-predicate")]
        public async Task SqlitePrincipalDirectoryRejectsConstraintRemovalOnReopen(string mutation)
        {
            var folder = Path.Combine(Path.GetTempPath(), "game-platform-cl101", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new AccountDirectoryScope(new BackendNamespace("test-backend"), App);
            try
            {
                var db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None);
                Assert.True(await db.DisposeAsync(TimeSpan.FromSeconds(5)));
                using (var raw = new SQLiteConnection(path))
                {
                    var migration = SqlitePrincipalDirectoryMigration.Create();
                    var tableSql = migration.Statements[0];
                    var indexSql = migration.Statements[1];
                    raw.Execute("DROP TABLE gp_account_principal_directory");
                    if (mutation == "installation-unique")
                        tableSql = tableSql.Replace(", UNIQUE(backend_namespace, app_id, installation_id)", string.Empty);
                    else if (mutation == "stream-unique")
                        tableSql = tableSql.Replace(", UNIQUE(backend_namespace, app_id, client_stream_id)", string.Empty);
                    else if (mutation == "issued-membership-check")
                        tableSql = tableSql.Replace(", CHECK((platform_user_id IS NULL AND membership_status IS NULL) OR (platform_user_id IS NOT NULL AND membership_status IS NOT NULL))", string.Empty);
                    else
                        indexSql += " AND 0";
                    raw.Execute(tableSql);
                    raw.Execute(indexSql);

                    const string insert = "INSERT INTO gp_account_principal_directory(backend_namespace,app_id,issuer,subject,installation_id,client_stream_id,platform_user_id,membership_status) VALUES (?,?,?,?,?,?,?,?)";
                    raw.Execute(insert, scope.BackendNamespace.Value, App.ToString(), "issuer", "subject-one", "installation-one", "stream-one",
                        mutation == "issued-index-extra-predicate" ? "issued-one" : null,
                        mutation == "issued-index-extra-predicate" ? "active" : null);
                    raw.Execute(insert, scope.BackendNamespace.Value, App.ToString(), "issuer", "subject-two",
                        mutation == "installation-unique" ? "installation-one" : "installation-two",
                        mutation == "stream-unique" ? "stream-one" : "stream-two",
                        mutation == "issued-index-extra-predicate" ? "issued-one" : mutation == "issued-membership-check" ? "issued-without-membership" : null,
                        mutation == "issued-index-extra-predicate" ? "active" : null);
                    Assert.Equal(2, raw.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_account_principal_directory"));
                }

                var error = await Assert.ThrowsAsync<StorageException>(() => SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None));
                Assert.Equal(StorageFailure.Migration, error.Failure);
            }
            finally { try { if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); } catch (IOException) { } }
        }

        [Fact]
        public async Task SqlitePrincipalDirectorySerializesConcurrentReservationsAndDrainsBeforeStop()
        {
            var folder = Path.Combine(Path.GetTempPath(), "game-platform-cl101", Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(folder); var path = Path.Combine(folder, "directory.sqlite3");
            var scope = new AccountDirectoryScope(new BackendNamespace("test-backend"), App);
            try
            {
                var db = await SqliteAccountsDirectoryDatabase.OpenAsync(path, scope, CancellationToken.None); var store = new SqliteAccountsDirectoryStore(db, scope);
                var reservations = new Task<AccountDirectoryEntry>[12];
                for (var index = 0; index < reservations.Length; index++)
                {
                    var suffix = (index + 1).ToString("x12");
                    var installation = Guid.Parse("0199f9a2-1111-7777-8888-" + suffix);
                    var stream = new ClientStreamId(Guid.Parse("0199f9a2-2222-7777-8888-" + suffix));
                    reservations[index] = store.ReserveAsync(A, App, installation, stream, CancellationToken.None);
                }
                var stopping = db.DisposeAsync(TimeSpan.FromSeconds(5));
                var entries = await Task.WhenAll(reservations);
                Assert.True(await stopping);
                Assert.All(entries, entry => { Assert.Equal(entries[0].InstallationId, entry.InstallationId); Assert.Equal(entries[0].StreamId, entry.StreamId); });
                Assert.Equal(0, db.ConnectionCount);
                await Assert.ThrowsAsync<StorageException>(async () => await store.FindAsync(A, App, CancellationToken.None));
            }
            finally { try { if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true); } catch (IOException) { } }
        }

        private static AccountsLifecycleService Service(IAccountsAuthLifecycle auth, Remote remote, Directory directory, Leases leases) => Service(auth, new ProvisioningFactory(_ => remote), directory, leases);
        private static AccountsLifecycleService Service(IAccountsAuthLifecycle auth, ProvisioningFactory factory, Directory directory, Leases leases) => new AccountsLifecycleService(auth, factory, directory, leases, new UuidV7Generator(new Clock(), new Random()));
        private static AccountsLifecycleService Service(IAccountsAuthLifecycle auth, Remote remote, Directory directory, Leases leases, Recovery recovery) => new AccountsLifecycleService(auth, new ProvisioningFactory(_ => remote), directory, leases, new UuidV7Generator(new Clock(), new Random()), null, recovery);
        private sealed class Recovery : IAccountsAuthRecovery { private readonly Task<AccountAuthLifecycleResult> result; public int Calls; public Recovery(AccountAuthLifecycleResult result) : this(Task.FromResult(result)) { } public Recovery(Task<AccountAuthLifecycleResult> result) { this.result = result; } public Task<AccountAuthLifecycleResult> RecoverAsync(CancellationToken token) { Interlocked.Increment(ref Calls); return result; } }
        private sealed class Auth : IAccountsAuthLifecycle { private readonly AccountAuthLifecycleResult result; public Auth(AccountPrincipalDescriptor principal) : this(AccountAuthLifecycleResult.Authenticated(principal, new Session())) { } public Auth(AccountAuthLifecycleResult result) { this.result = result; } public Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken token) => Task.FromResult(result); }
        private sealed class PendingAuth : IAccountsAuthLifecycle { private readonly Task<AccountAuthLifecycleResult> result; public PendingAuth(Task<AccountAuthLifecycleResult> result) { this.result = result; } public Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken token) => result; }
        private sealed class SequenceAuth : IAccountsAuthLifecycle { private readonly Queue<Task<AccountAuthLifecycleResult>> values; public SequenceAuth(params Task<AccountAuthLifecycleResult>[] values) { this.values = new Queue<Task<AccountAuthLifecycleResult>>(values); } public Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken token) => values.Dequeue(); }
        private sealed class FirstCancellationThenAuthenticated : IAccountsAuthLifecycle { private int calls; public async Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken token) { if (Interlocked.Increment(ref calls) == 1) { await Task.Delay(Timeout.Infinite, token); } return AccountAuthLifecycleResult.Authenticated(B, new Session()); } }
        private sealed class Session : IAuthSession { public string SessionKey => "test-session"; public Task<AccessTokenSnapshot> GetAsync(CancellationToken token) => Task.FromResult(new AccessTokenSnapshot("opaque", 0)); public Task<AccessTokenSnapshot> RefreshAsync(long generation, CancellationToken token) => GetAsync(token); }
        private sealed class ProvisioningFactory : IProvisioningRemoteFactory { private readonly Func<IAuthSession, IProvisioningRemote> create; public ProvisioningFactory(Func<IAuthSession, IProvisioningRemote> create) { this.create = create; } public int Calls; public IAuthSession? LastSession; public IProvisioningRemote Create(IAuthSession authenticatedSession) { Calls++; LastSession = authenticatedSession; return create(authenticatedSession); } }
        private sealed class Remote : IProvisioningRemote { public int Calls; public bool FailFirst; public Task<RemoteResult<ProvisioningSnapshot>> ProvisionAsync(AppId app, Guid installation, ClientStreamId stream, CancellationToken token) { Calls++; if (FailFirst && Calls == 1) return Task.FromResult(RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.OutcomeUncertain))); return Task.FromResult(RemoteResult<ProvisioningSnapshot>.Success(new ProvisioningSnapshot(new PlatformUserId(Guid.Parse("0199f9a0-2222-7777-8888-999999999999")), app, "active", stream, 1, 1))); } }
        private sealed class Directory : IAccountDirectoryStore { private readonly Dictionary<string, AccountDirectoryEntry> values = new Dictionary<string, AccountDirectoryEntry>(); private static string Key(AccountPrincipalDescriptor p, AppId a) => p.BackendNamespace + ":" + a + ":" + p.Issuer + ":" + p.Subject; public Task<bool> HasAnyEntryAsync(CancellationToken t) => Task.FromResult(values.Count != 0); public Task<AccountDirectoryEntry?> FindAsync(AccountPrincipalDescriptor p, AppId a, CancellationToken t) => Task.FromResult(values.TryGetValue(Key(p, a), out var value) ? value : null); public Task<AccountDirectoryEntry> ReserveAsync(AccountPrincipalDescriptor p, AppId a, Guid i, ClientStreamId s, CancellationToken t) { var key = Key(p, a); if (!values.TryGetValue(key, out var value)) values[key] = value = new AccountDirectoryEntry(p, a, i, s, null, null); return Task.FromResult(value); } public Task<AccountDirectoryEntry> BindIssuedAccountAsync(AccountDirectoryEntry r, PlatformUserId id, string membership, CancellationToken t) { var value = new AccountDirectoryEntry(r.Principal, r.AppId, r.InstallationId, r.StreamId, id, membership); values[Key(r.Principal, r.AppId)] = value; return Task.FromResult(value); } }
        private sealed class Leases : IAccountScopeLeaseFactory { private readonly List<string> events = new List<string>(); public bool Ready = true; public AccountBootstrapResult Bootstrap = AccountBootstrapResult.Complete; public int DrainFailures; public int Opened; public int Retired; public int Stopped; public IAuthSession? LastAuthenticatedSession; public TaskCompletionSource<bool>? FirstOpenGate; public TaskCompletionSource<bool>? FirstLeaseOpened; public TaskCompletionSource<bool>? FirstDrainGate; public TaskCompletionSource<bool>? FirstDrainStarted; public async Task<IAccountScopeLease> OpenAuthenticatedAsync(AccountDirectoryEntry e, IAuthSession s, long g, CancellationToken t) { LastAuthenticatedSession = s; var lease = new Lease(e, g, Ready, Bootstrap, this); if (lease.Ordinal == 1 && FirstOpenGate != null) { FirstLeaseOpened!.SetResult(true); await FirstOpenGate.Task.ConfigureAwait(false); } return lease; } public Task<IAccountScopeLease> ReopenOfflineAsync(AccountDirectoryEntry e, long g, CancellationToken t) => Task.FromResult<IAccountScopeLease>(new Lease(e, g, Ready, Bootstrap, this)); public int EventIndex(string value) { lock (events) return events.IndexOf(value); } public void OpenedEvent(int ordinal) { lock (events) events.Add("open-" + ordinal); } public void RetiredEvent(int ordinal) { lock (events) events.Add("retire-" + ordinal); } }
        private sealed class Lease : IAccountScopeLease { private readonly bool ready; private readonly AccountBootstrapResult bootstrap; private readonly Leases owner; public Lease(AccountDirectoryEntry e, long g, bool ready, AccountBootstrapResult bootstrap, Leases owner) { Entry = e; Generation = g; this.ready = ready; this.bootstrap = bootstrap; this.owner = owner; Ordinal = Interlocked.Increment(ref owner.Opened); owner.OpenedEvent(Ordinal); } public int Ordinal { get; } public AccountDirectoryEntry Entry { get; } public long Generation { get; } public Task<bool> IsBootstrapReadyAsync(CancellationToken t) => Task.FromResult(ready); public Task<AccountBootstrapResult> BootstrapAsync(CancellationToken t) => Task.FromResult(bootstrap); public Task StopAdmissionsAsync(CancellationToken t) { Interlocked.Increment(ref owner.Stopped); return Task.CompletedTask; } public async Task DrainAndRetireAsync(CancellationToken t) { if (Ordinal == 1 && owner.FirstDrainGate != null) { owner.FirstDrainStarted!.SetResult(true); await owner.FirstDrainGate.Task.ConfigureAwait(false); } if (Interlocked.CompareExchange(ref owner.DrainFailures, 0, 0) > 0) { Interlocked.Decrement(ref owner.DrainFailures); throw new InvalidOperationException("drain failure"); } Interlocked.Increment(ref owner.Retired); owner.RetiredEvent(Ordinal); } }
        private sealed class Clock : IUnixMillisecondClock { public long GetUnixMilliseconds() => 1; }
        private sealed class Random : IUuidRandomSource { public void Fill(Span<byte> destination) { for (var i = 0; i < destination.Length; i++) destination[i] = (byte)(i + 1); } }
    }
}
