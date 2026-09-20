#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions.Accounts;

namespace GamePlatform.Features.Accounts
{
    public enum AccountAuthLifecycleKind { Authenticated, UnavailableOffline, Cancelled, RecoveryRequired }
    public enum AccountsReadiness { NeverProvisioned, Authenticating, Provisioning, Bootstrapping, Ready, UnavailableOffline, RecoveryRequired, Cancelled, Unavailable, LateResultRejected }
    public enum AccountBootstrapResult { Complete, Incomplete, RemoteFailure }

    /// <summary>Host-owned credential result. The descriptor is non-secret; the session is retained only in memory.</summary>
    public sealed class AccountAuthLifecycleResult
    {
        private AccountAuthLifecycleResult(AccountAuthLifecycleKind kind, AccountPrincipalDescriptor? principal, IAuthSession? session)
        {
            if (!Enum.IsDefined(typeof(AccountAuthLifecycleKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == AccountAuthLifecycleKind.Authenticated && (principal == null || session == null)) throw new ArgumentException("Authenticated requires a principal-specific session.");
            if (kind != AccountAuthLifecycleKind.Authenticated && session != null) throw new ArgumentException("Only authenticated results carry a session.");
            Kind = kind; Principal = principal; Session = session;
        }
        public AccountAuthLifecycleKind Kind { get; }
        public AccountPrincipalDescriptor? Principal { get; }
        internal IAuthSession? Session { get; }
        public static AccountAuthLifecycleResult Authenticated(AccountPrincipalDescriptor principal, IAuthSession session) => new AccountAuthLifecycleResult(AccountAuthLifecycleKind.Authenticated, principal, session);
        public static AccountAuthLifecycleResult UnavailableOffline(AccountPrincipalDescriptor? knownPrincipal) => new AccountAuthLifecycleResult(AccountAuthLifecycleKind.UnavailableOffline, knownPrincipal, null);
        public static AccountAuthLifecycleResult Cancelled() => new AccountAuthLifecycleResult(AccountAuthLifecycleKind.Cancelled, null, null);
        public static AccountAuthLifecycleResult RecoveryRequired(AccountPrincipalDescriptor? knownPrincipal) => new AccountAuthLifecycleResult(AccountAuthLifecycleKind.RecoveryRequired, knownPrincipal, null);
    }

    public interface IAccountsAuthLifecycle { Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken cancellationToken); }

    /// <summary>One account-private scope; its implementation reads actual private-sync readiness and owns admission draining.</summary>
    public interface IAccountScopeLease
    {
        AccountDirectoryEntry Entry { get; }
        long Generation { get; }
        Task<bool> IsBootstrapReadyAsync(CancellationToken cancellationToken);
        Task<AccountBootstrapResult> BootstrapAsync(CancellationToken cancellationToken);
        Task StopAdmissionsAsync(CancellationToken cancellationToken);
        Task DrainAndRetireAsync(CancellationToken cancellationToken);
    }

    public interface IAccountScopeLeaseFactory
    {
        Task<IAccountScopeLease> OpenAuthenticatedAsync(AccountDirectoryEntry entry, IAuthSession principalSession, long generation, CancellationToken cancellationToken);
        Task<IAccountScopeLease> ReopenOfflineAsync(AccountDirectoryEntry entry, long generation, CancellationToken cancellationToken);
    }

    public sealed class AccountsLifecycleSnapshot
    {
        internal AccountsLifecycleSnapshot(AccountsReadiness readiness, AccountDirectoryEntry? entry, long generation) { Readiness = readiness; Entry = entry; Generation = generation; }
        public AccountsReadiness Readiness { get; }
        public AccountDirectoryEntry? Entry { get; }
        public long Generation { get; }
    }

    /// <summary>Portable account lifecycle. It persists reservation before provisioning and never derives a principal from a session key.</summary>
    public sealed class AccountsLifecycleService
    {
        private readonly IAccountsAuthLifecycle auth; private readonly IProvisioningRemote provisioning; private readonly IAccountDirectoryStore directory; private readonly IAccountScopeLeaseFactory scopes; private readonly UuidV7Generator ids; private readonly TimeSpan lateLeaseDrainTimeout;
        private readonly SemaphoreSlim activation = new SemaphoreSlim(1, 1); private readonly object gate = new object();
        private long generation; private AccountsReadiness readiness = AccountsReadiness.NeverProvisioned; private AccountDirectoryEntry? current; private IAccountScopeLease? active; private IAccountScopeLease? failedRetirement; private bool scopeRecoveryRequired;
        private bool stopped; private TaskCompletionSource<AccountsLifecycleSnapshot>? stoppedCompletion;

        public AccountsLifecycleService(IAccountsAuthLifecycle auth, IProvisioningRemote provisioning, IAccountDirectoryStore directory, IAccountScopeLeaseFactory scopes, UuidV7Generator ids, TimeSpan? lateLeaseDrainTimeout = null)
        {
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth)); this.provisioning = provisioning ?? throw new ArgumentNullException(nameof(provisioning)); this.directory = directory ?? throw new ArgumentNullException(nameof(directory)); this.scopes = scopes ?? throw new ArgumentNullException(nameof(scopes)); this.ids = ids ?? throw new ArgumentNullException(nameof(ids));
            this.lateLeaseDrainTimeout = lateLeaseDrainTimeout ?? TimeSpan.FromSeconds(5);
            if (this.lateLeaseDrainTimeout < TimeSpan.FromMilliseconds(1) || this.lateLeaseDrainTimeout > TimeSpan.FromSeconds(30)) throw new ArgumentOutOfRangeException(nameof(lateLeaseDrainTimeout));
        }

        public AccountsLifecycleSnapshot Snapshot { get { lock (gate) return new AccountsLifecycleSnapshot(readiness, current, generation); } }

        /// <summary>
        /// Permanently fences the current generation and synchronously retires any
        /// admitted account scope. Caller cancellation cannot leave an active writer:
        /// retirement uses the lifecycle's own bounded timeout token.
        /// </summary>
        public async Task<AccountsLifecycleSnapshot> StopAsync(CancellationToken cancellationToken)
        {
            IAccountScopeLease? retiring;
            AccountDirectoryEntry? entry;
            long request;
            Task<AccountsLifecycleSnapshot>? existing;
            TaskCompletionSource<AccountsLifecycleSnapshot>? completion;
            lock (gate)
            {
                if (stoppedCompletion != null) { existing = stoppedCompletion.Task; retiring = null; entry = null; request = generation; completion = null; }
                else
                {
                existing = null; completion = stoppedCompletion = new TaskCompletionSource<AccountsLifecycleSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                stopped = true; generation = checked(generation + 1); request = generation; entry = current;
                retiring = active ?? failedRetirement;
                active = null;
                if (retiring != null) { failedRetirement = retiring; scopeRecoveryRequired = true; readiness = AccountsReadiness.RecoveryRequired; }
                else { current = null; readiness = AccountsReadiness.Unavailable; }
                }
            }
            if (existing != null) return await existing.ConfigureAwait(false);
            AccountsLifecycleSnapshot outcome;
            // Do not use the caller token here. Once stop is accepted, it must quiesce
            // an admitted writer even when the caller has already been cancelled.
            var activationHeld = false;
            try
            {
                using var timeout = new CancellationTokenSource(lateLeaseDrainTimeout);
                try { await activation.WaitAsync(timeout.Token).ConfigureAwait(false); activationHeld = true; }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    lock (gate) { scopeRecoveryRequired = true; readiness = AccountsReadiness.RecoveryRequired; outcome = new AccountsLifecycleSnapshot(readiness, current, request); }
                    completion!.TrySetResult(outcome); return outcome;
                }
                if (retiring == null) outcome = ScopeRecoveryRequired() ? Capture(request, AccountsReadiness.RecoveryRequired, current) : Capture(request, AccountsReadiness.Unavailable, null);
                else
                {
                var retired = await RetireLeaseAsync(retiring).ConfigureAwait(false);
                lock (gate)
                {
                    if (ReferenceEquals(failedRetirement, retiring)) failedRetirement = retired ? null : retiring;
                    scopeRecoveryRequired = !retired;
                    current = retired ? null : entry;
                    readiness = retired ? AccountsReadiness.Unavailable : AccountsReadiness.RecoveryRequired;
                    outcome = new AccountsLifecycleSnapshot(readiness, current, request);
                }
                }
            }
            catch { lock (gate) { readiness = AccountsReadiness.RecoveryRequired; outcome = new AccountsLifecycleSnapshot(readiness, current, request); } }
            finally { if (activationHeld) activation.Release(); }
            completion!.TrySetResult(outcome); return outcome;
        }

        public async Task<AccountsLifecycleSnapshot> StartAsync(AppId appId, CancellationToken cancellationToken)
        {
            if (!appId.IsValid) throw new ArgumentException("A valid app is required.", nameof(appId));
            lock (gate) if (stopped) return new AccountsLifecycleSnapshot(AccountsReadiness.Unavailable, null, generation);
            var request = Begin(AccountsReadiness.Authenticating);
            AccountAuthLifecycleResult result;
            try { result = await auth.AuthenticateAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Finish(request, AccountsReadiness.Cancelled, null); }
            if (!Current(request)) return Late(request);
            if (result.Kind == AccountAuthLifecycleKind.Cancelled) return Finish(request, AccountsReadiness.Cancelled, null);
            if (result.Kind == AccountAuthLifecycleKind.RecoveryRequired) return Finish(request, AccountsReadiness.RecoveryRequired, null);
            if (result.Kind == AccountAuthLifecycleKind.UnavailableOffline) return await ReopenOfflineAsync(request, appId, result.Principal, cancellationToken).ConfigureAwait(false);
            return await ProvisionAndBootstrapAsync(request, appId, result.Principal!, result.Session!, cancellationToken).ConfigureAwait(false);
        }

        private async Task<AccountsLifecycleSnapshot> ReopenOfflineAsync(long request, AppId appId, AccountPrincipalDescriptor? principal, CancellationToken token)
        {
            if (principal == null) return Finish(request, AccountsReadiness.UnavailableOffline, null);
            var entry = await directory.FindAsync(principal, appId, token).ConfigureAwait(false);
            if (!Current(request)) return Late(request);
            if (entry == null) return Finish(request, AccountsReadiness.NeverProvisioned, null);
            if (!entry.HasIssuedAccount) return Finish(request, AccountsReadiness.UnavailableOffline, entry);
            return await ActivateOfflineAsync(request, entry, token).ConfigureAwait(false);
        }

        private async Task<AccountsLifecycleSnapshot> ActivateOfflineAsync(long request, AccountDirectoryEntry entry, CancellationToken token)
        {
            await activation.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!Current(request)) return Late(request);
                if (ScopeRecoveryRequired()) return Finish(request, AccountsReadiness.RecoveryRequired, entry);
                if (!await RetireActiveAsync(request).ConfigureAwait(false)) return Quarantine(request, entry);
                token.ThrowIfCancellationRequested();
                var lease = await scopes.ReopenOfflineAsync(entry, request, token).ConfigureAwait(false);
                if (!Current(request)) { if (!await RetireLateLeaseAsync(lease).ConfigureAwait(false)) Quarantine(lease); return Late(request); }
                if (!await lease.IsBootstrapReadyAsync(token).ConfigureAwait(false)) { if (!await RetireLateLeaseAsync(lease).ConfigureAwait(false)) return Quarantine(request, entry, lease); return Finish(request, AccountsReadiness.UnavailableOffline, entry); }
                SetActive(request, lease, entry, AccountsReadiness.Ready); return Capture(request, AccountsReadiness.Ready, entry);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return Finish(request, AccountsReadiness.Unavailable, entry); }
            finally { activation.Release(); }
        }

        private async Task<AccountsLifecycleSnapshot> ProvisionAndBootstrapAsync(long request, AppId appId, AccountPrincipalDescriptor principal, IAuthSession session, CancellationToken token)
        {
            var entry = await directory.FindAsync(principal, appId, token).ConfigureAwait(false);
            if (!Current(request)) return Late(request);
            if (entry == null)
            {
                var installation = ids.NewId(); var stream = new ClientStreamId(ids.NewId());
                entry = await directory.ReserveAsync(principal, appId, installation, stream, token).ConfigureAwait(false);
                if (!Current(request)) return Late(request);
            }
            Set(request, AccountsReadiness.Provisioning, entry);
            var provisioned = await provisioning.ProvisionAsync(appId, entry.InstallationId, entry.StreamId, token).ConfigureAwait(false);
            if (!Current(request)) return Late(request);
            if (!provisioned.IsSuccess) return Finish(request, provisioned.Failure.Kind == RemoteFailureKind.Authentication ? AccountsReadiness.RecoveryRequired : AccountsReadiness.Unavailable, entry);
            var value = provisioned.Value!;
            if (value.AppId != appId || value.StreamId != entry.StreamId) return Finish(request, AccountsReadiness.RecoveryRequired, entry);
            if (entry.HasIssuedAccount && entry.AccountId != value.AccountId) return Finish(request, AccountsReadiness.RecoveryRequired, entry);
            entry = await directory.BindIssuedAccountAsync(entry, value.AccountId, value.MembershipStatus, token).ConfigureAwait(false);
            if (!Current(request)) return Late(request);
            return await ActivateAuthenticatedAsync(request, entry, session, token).ConfigureAwait(false);
        }

        private async Task<AccountsLifecycleSnapshot> ActivateAuthenticatedAsync(long request, AccountDirectoryEntry entry, IAuthSession session, CancellationToken token)
        {
            await activation.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!Current(request)) return Late(request);
                if (ScopeRecoveryRequired()) return Finish(request, AccountsReadiness.RecoveryRequired, entry);
                if (!await RetireActiveAsync(request).ConfigureAwait(false)) return Quarantine(request, entry);
                token.ThrowIfCancellationRequested();
                Set(request, AccountsReadiness.Bootstrapping, entry);
                var lease = await scopes.OpenAuthenticatedAsync(entry, session, request, token).ConfigureAwait(false);
                if (!Current(request)) { if (!await RetireLateLeaseAsync(lease).ConfigureAwait(false)) Quarantine(lease); return Late(request); }
                var bootstrap = await lease.BootstrapAsync(token).ConfigureAwait(false);
                if (!Current(request)) { if (!await RetireLateLeaseAsync(lease).ConfigureAwait(false)) Quarantine(lease); return Late(request); }
                if (bootstrap != AccountBootstrapResult.Complete || !await lease.IsBootstrapReadyAsync(token).ConfigureAwait(false)) { if (!await RetireLateLeaseAsync(lease).ConfigureAwait(false)) return Quarantine(request, entry, lease); return Finish(request, AccountsReadiness.Bootstrapping, entry); }
                SetActive(request, lease, entry, AccountsReadiness.Ready); return Capture(request, AccountsReadiness.Ready, entry);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return Finish(request, AccountsReadiness.Unavailable, entry); }
            finally { activation.Release(); }
        }

        private async Task<bool> RetireActiveAsync(long request)
        {
            IAccountScopeLease? old;
            lock (gate)
            {
                if (generation != request || failedRetirement != null) return false;
                old = active;
                if (old != null)
                {
                    // This is the irreversible switch point. The old scope is never active again.
                    active = null; failedRetirement = old; scopeRecoveryRequired = true; readiness = AccountsReadiness.RecoveryRequired;
                }
            }
            if (old == null) return true;
            if (!await RetireLeaseAsync(old).ConfigureAwait(false)) return false;
            lock (gate)
            {
                if (ReferenceEquals(failedRetirement, old)) failedRetirement = null;
                if (generation == request) scopeRecoveryRequired = false;
                return generation == request;
            }
        }
        private async Task<bool> RetireLateLeaseAsync(IAccountScopeLease lease)
            => await RetireLeaseAsync(lease).ConfigureAwait(false);
        private async Task<bool> RetireLeaseAsync(IAccountScopeLease lease)
        {
            using var timeout = new CancellationTokenSource(lateLeaseDrainTimeout);
            try
            {
                if (!await CompleteWithinAsync(lease.StopAdmissionsAsync(timeout.Token), timeout.Token).ConfigureAwait(false)) return false;
                return await CompleteWithinAsync(lease.DrainAndRetireAsync(timeout.Token), timeout.Token).ConfigureAwait(false);
            }
            catch { return false; }
        }
        private static async Task<bool> CompleteWithinAsync(Task operation, CancellationToken timeout)
        {
            var completed = await Task.WhenAny(operation, Task.Delay(Timeout.InfiniteTimeSpan, timeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, operation)) return false;
            await operation.ConfigureAwait(false); return true;
        }
        private long Begin(AccountsReadiness next) { lock (gate) { generation = checked(generation + 1); readiness = next; current = null; return generation; } }
        private bool Current(long request) { lock (gate) return generation == request; }
        private AccountsLifecycleSnapshot Late(long request) => new AccountsLifecycleSnapshot(AccountsReadiness.LateResultRejected, null, request);
        private AccountsLifecycleSnapshot Capture(long request, AccountsReadiness next, AccountDirectoryEntry? entry)
        {
            lock (gate) return generation == request ? new AccountsLifecycleSnapshot(next, entry, request) : Late(request);
        }
        private AccountsLifecycleSnapshot Finish(long request, AccountsReadiness next, AccountDirectoryEntry? entry)
        {
            lock (gate)
            {
                if (generation != request) return Late(request);
                readiness = next; current = entry;
                return new AccountsLifecycleSnapshot(next, entry, request);
            }
        }
        private bool ScopeRecoveryRequired() { lock (gate) return scopeRecoveryRequired; }
        private void Quarantine(IAccountScopeLease? failedLease = null) { lock (gate) { if (failedLease != null) { active = null; failedRetirement = failedLease; } scopeRecoveryRequired = true; readiness = AccountsReadiness.RecoveryRequired; } }
        private AccountsLifecycleSnapshot Quarantine(long request, AccountDirectoryEntry? entry, IAccountScopeLease? failedLease = null) { Quarantine(failedLease); return Capture(request, AccountsReadiness.RecoveryRequired, entry); }
        private void Set(long request, AccountsReadiness next, AccountDirectoryEntry? entry) { lock (gate) { if (generation == request) { readiness = next; current = entry; } } }
        private void SetActive(long request, IAccountScopeLease lease, AccountDirectoryEntry entry, AccountsReadiness next) { lock (gate) { if (generation == request) { active = lease; current = entry; readiness = next; } } }
    }
}
