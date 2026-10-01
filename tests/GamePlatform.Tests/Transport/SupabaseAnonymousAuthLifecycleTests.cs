#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Accounts;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Transport.Http.Supabase;
namespace GamePlatform.Tests.Transport
{
    public sealed class SupabaseAnonymousAuthLifecycleTests
    {
        static readonly Guid Subject = Guid.Parse("019a1234-1111-7777-8888-999999999999");
        [Fact] public async Task AbsentMarkerIsRecoveryAndSendsNothing() { var x = new Scripted(Response()); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(x, new Store()).AuthenticateAsync(CancellationToken.None)).Kind); Assert.Empty(x.Requests); }
        [Fact] public async Task ExplicitFreshAuthorizationAllowsOneSignupAndPublicMarkerHasNoSecret() { var s = new Store(); var p = P(new Scripted(Response()), s); Assert.True(await p.AuthorizeFreshAsync(CancellationToken.None)); Assert.False(await p.AuthorizeFreshAsync(CancellationToken.None)); var r = await p.AuthenticateAsync(CancellationToken.None); Assert.Equal(AccountAuthLifecycleKind.Authenticated, r.Kind); Assert.Equal(SupabaseAnonymousSessionState.Known, s.Current.Public!.State); Assert.DoesNotContain("refresh", s.Current.Public.Subject); Assert.Contains("refresh", Encoding.UTF8.GetString(s.Current.CopySecret()!)); Assert.DoesNotContain("access", Encoding.UTF8.GetString(s.Current.CopySecret()!)); }
        [Fact] public async Task MalformedSignupAndSubjectMismatchAreRecoveryWithoutReplacement() { var malformed = new Store(); var p = P(new Scripted(new HttpResponseData(200, new Dictionary<string, string> { { "Content-Type", "application/json" } }, Encoding.UTF8.GetBytes("{bad"))), malformed); Assert.True(await p.AuthorizeFreshAsync(CancellationToken.None)); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await p.AuthenticateAsync(CancellationToken.None)).Kind); Assert.Equal(SupabaseAnonymousSessionState.RecoveryRequired, malformed.Current.Public!.State); Assert.Null(malformed.Current.Public.Subject); Assert.Null(malformed.Current.CopySecret()); var known = await Known(); long knownVersion = known.Current.Public!.Version; byte[] originalSecret = known.Current.CopySecret()!; var mismatch = new HttpResponseData(200, new Dictionary<string, string> { { "Content-Type", "application/json" } }, Encoding.UTF8.GetBytes("{\"access_token\":\"a\",\"refresh_token\":\"r\",\"expires_at\":2000,\"user\":{\"id\":\"019a1234-2222-7777-8888-999999999999\"}}")); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(new Scripted(mismatch), known).AuthenticateAsync(CancellationToken.None)).Kind); Assert.Equal(SupabaseAnonymousSessionState.RecoveryRequired, known.Current.Public!.State); Assert.Equal(Subject.ToString("D"), known.Current.Public.Subject); Assert.Equal(knownVersion + 2, known.Current.Public.Version); Assert.Equal(originalSecret, known.Current.CopySecret()); var pending = known.Writes[^2]; Assert.Equal(SupabaseAnonymousSessionState.RefreshPending, pending.Public!.State); Assert.Equal(pending.Public.Version + 1, known.Current.Public.Version); Assert.Equal(pending.Public.Subject, known.Current.Public.Subject); Assert.Equal(pending.CopySecret(), known.Current.CopySecret()); var restart = new Scripted(); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(restart, known).AuthenticateAsync(CancellationToken.None)).Kind); Assert.Empty(restart.Requests); }
        [Fact] public async Task ExpiredSessionRefreshesThroughVersionedRotation() { var store = new Store(); var provider = P(new Scripted(Response("old", "refresh1", 1), Response("new", "refresh2", 3000)), store); Assert.True(await provider.AuthorizeFreshAsync(CancellationToken.None)); var authenticated = await provider.AuthenticateAsync(CancellationToken.None); var token = await authenticated.Session!.GetAsync(CancellationToken.None); Assert.Equal("new", token.Value); Assert.Equal(1, token.Generation); Assert.Contains("refresh2", Encoding.UTF8.GetString(store.Current.CopySecret()!)); }
        [Fact] public async Task UncertainRefreshPersistsRecoveryMarkerAndRestartDoesNotSend() { var store = await Known(); long knownVersion = store.Current.Public!.Version; byte[] originalSecret = store.Current.CopySecret()!; var first = new Scripted(new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "test")); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(first, store).AuthenticateAsync(CancellationToken.None)).Kind); Assert.Single(first.Requests); Assert.Equal(SupabaseAnonymousSessionState.RecoveryRequired, store.Current.Public!.State); Assert.Equal(Subject.ToString("D"), store.Current.Public.Subject); Assert.Equal(knownVersion + 2, store.Current.Public.Version); Assert.Equal(originalSecret, store.Current.CopySecret()); var pending = store.Writes[^2]; Assert.Equal(SupabaseAnonymousSessionState.RefreshPending, pending.Public!.State); Assert.Equal(pending.Public.Version + 1, store.Current.Public.Version); Assert.Equal(pending.Public.Subject, store.Current.Public.Subject); Assert.Equal(pending.CopySecret(), store.Current.CopySecret()); var restart = new Scripted(); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(restart, store).AuthenticateAsync(CancellationToken.None)).Kind); Assert.Empty(restart.Requests); }
        [Fact] public async Task PendingRestartAndKnownMissingSecretAreRecoveryWithoutNewSignup() { var s = new Store(); var p = P(new Scripted(new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "test")), s); Assert.True(await p.AuthorizeFreshAsync(CancellationToken.None)); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await p.AuthenticateAsync(CancellationToken.None)).Kind); Assert.Equal(SupabaseAnonymousSessionState.RecoveryRequired, s.Current.Public!.State); Assert.Null(s.Current.Public.Subject); Assert.Null(s.Current.CopySecret()); var x = new Scripted(); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(x, s).AuthenticateAsync(CancellationToken.None)).Kind); Assert.Empty(x.Requests); var k = new Store(); await k.Force(SupabaseAnonymousSessionState.Known, Subject.ToString("D"), null); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(new Scripted(), k).AuthenticateAsync(CancellationToken.None)).Kind); }
        [Fact] public async Task NotSentVersionedCleanupRestoresFreshButUncertainDoesNot() { var s = new Store(); var p = P(new Scripted(new HttpExecutionException(HttpDeliveryCertainty.NotSent, "x")), s); Assert.True(await p.AuthorizeFreshAsync(CancellationToken.None)); Assert.Equal(AccountAuthLifecycleKind.UnavailableOffline, (await p.AuthenticateAsync(CancellationToken.None)).Kind); Assert.Equal(SupabaseAnonymousSessionState.FreshAuthorized, s.Current.Public!.State); Assert.True(s.Current.Public.Version >= 3); var u = new Store(); var q = P(new Scripted(new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "x")), u); Assert.True(await q.AuthorizeFreshAsync(CancellationToken.None)); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await q.AuthenticateAsync(CancellationToken.None)).Kind); Assert.Equal(SupabaseAnonymousSessionState.RecoveryRequired, u.Current.Public!.State); }
        [Fact] public async Task SignupNotSentCleanupConflictStoreFailureAndCancellationAreFailClosed() { var conflict = new Store { RejectOnCall = 3 }; var p = P(new Scripted(new HttpExecutionException(HttpDeliveryCertainty.NotSent, "x")), conflict); Assert.True(await p.AuthorizeFreshAsync(CancellationToken.None)); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await p.AuthenticateAsync(CancellationToken.None)).Kind); var failure = new Store { ThrowOnCall = 3 }; p = P(new Scripted(new HttpExecutionException(HttpDeliveryCertainty.NotSent, "x")), failure); Assert.True(await p.AuthorizeFreshAsync(CancellationToken.None)); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await p.AuthenticateAsync(CancellationToken.None)).Kind); using var cancelled = new CancellationTokenSource(); var cancellation = new Store { CancelOnCall = 3, Cancellation = cancelled }; p = P(new Scripted(new HttpExecutionException(HttpDeliveryCertainty.NotSent, "x")), cancellation); Assert.True(await p.AuthorizeFreshAsync(CancellationToken.None)); Assert.Equal(AccountAuthLifecycleKind.Cancelled, (await p.AuthenticateAsync(cancelled.Token)).Kind); }
        [Fact] public async Task SessionNotSentCleanupConflictStoreFailureAndCancellationAreFailClosed() { foreach (var mode in new[] { 1, 2 }) { var store = new Store(); var provider = P(new Scripted(Response("old", "refresh1", 1), new HttpExecutionException(HttpDeliveryCertainty.NotSent, "x")), store); Assert.True(await provider.AuthorizeFreshAsync(CancellationToken.None)); var session = (await provider.AuthenticateAsync(CancellationToken.None)).Session!; if (mode == 1) store.RejectOnCall = store.Calls + 2; else store.ThrowOnCall = store.Calls + 2; var error = await Assert.ThrowsAsync<InvalidOperationException>(() => session.GetAsync(CancellationToken.None)); Assert.Contains("recovery", error.Message, StringComparison.OrdinalIgnoreCase); } using var cancelled = new CancellationTokenSource(); var cstore = new Store { Cancellation = cancelled }; var cp = P(new Scripted(Response("old", "refresh1", 1), new HttpExecutionException(HttpDeliveryCertainty.NotSent, "x")), cstore); Assert.True(await cp.AuthorizeFreshAsync(CancellationToken.None)); var cs = (await cp.AuthenticateAsync(CancellationToken.None)).Session!; cstore.CancelOnCall = cstore.Calls + 2; await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cs.GetAsync(cancelled.Token)); }
        [Fact] public async Task RefreshRotatesWinnerAndStoreConflictPublishesNoSession() { var s = await Known(); var gate = new TaskCompletionSource<HttpResponseData>(TaskCreationOptions.RunContinuationsAsynchronously); var firstExecutor = new Scripted(gate.Task); var a = P(firstExecutor, s).AuthenticateAsync(CancellationToken.None); await firstExecutor.FirstSend.Task; var b = await P(new Scripted(Response("bad", "bad")), s).AuthenticateAsync(CancellationToken.None); gate.SetResult(Response("access2", "refresh2")); var ar = await a; Assert.Equal(AccountAuthLifecycleKind.Authenticated, ar.Kind); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, b.Kind); Assert.Contains("refresh2", Encoding.UTF8.GetString(s.Current.CopySecret()!)); var c = await Known(); c.RejectKnown = true; Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(new Scripted(Response("x", "y")), c).AuthenticateAsync(CancellationToken.None)).Kind); }
        [Fact] public async Task CancellationUnavailableOverflowAndNullSnapshotFailClosed() { var s = new Store(); using var c = new CancellationTokenSource(); c.Cancel(); Assert.Equal(AccountAuthLifecycleKind.Cancelled, (await P(new Scripted(), s).AuthenticateAsync(c.Token)).Kind); s.Available = false; Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(new Scripted(), s).AuthenticateAsync(CancellationToken.None)).Kind); var o = new Store(); await o.Force(SupabaseAnonymousSessionState.Known, Subject.ToString("D"), new byte[16385]); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(new Scripted(), o).AuthenticateAsync(CancellationToken.None)).Kind); o.NullRead = true; Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(new Scripted(), o).AuthenticateAsync(CancellationToken.None)).Kind); }
        [Fact] public async Task InvalidOrOverflowSnapshotAndMalformedStoreSuccessAreRecovery() { Assert.Throws<ArgumentOutOfRangeException>(() => new SupabaseAnonymousSessionSnapshot(new SupabaseAnonymousSessionPublicRecord(0, SupabaseAnonymousSessionState.Known, Subject.ToString("D")), null)); Assert.Throws<ArgumentOutOfRangeException>(() => new SupabaseAnonymousSessionPublicRecord(1, (SupabaseAnonymousSessionState)999, null)); var overflow = new Store { Current = new SupabaseAnonymousSessionSnapshot(new SupabaseAnonymousSessionPublicRecord(long.MaxValue, SupabaseAnonymousSessionState.FreshAuthorized, null), null) }; Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(new Scripted(Response()), overflow).AuthenticateAsync(CancellationToken.None)).Kind); Assert.Empty(new Scripted().Requests); var corrupt = new Store { ReturnMaxOnSuccess = true }; var p = P(new Scripted(Response()), corrupt); Assert.False(await p.AuthorizeFreshAsync(CancellationToken.None)); }
        [Fact] public async Task BoundedExecutorRejectsAuthorityRouteRedirectAndOversize() { var cfg = C(1024); var h = new Handler((r, _) => r.RequestUri!.AbsolutePath.EndsWith("signup", StringComparison.Ordinal) ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect) { RequestMessage = r }) : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = r, Content = new ByteArrayContent(new byte[1025]) })); using var e = new BoundedSupabaseAuthHttpExecutor(cfg, h); await Assert.ThrowsAsync<HttpExecutionException>(() => e.SendAsync(new HttpRequestData("POST", "https://other.example.test/auth/v1/signup", H(), Encoding.UTF8.GetBytes("{}")), CancellationToken.None)); await Assert.ThrowsAsync<HttpExecutionException>(() => e.SendAsync(new HttpRequestData("POST", cfg.Resolve("bad").AbsoluteUri, H(), Encoding.UTF8.GetBytes("{}")), CancellationToken.None)); await Assert.ThrowsAsync<HttpExecutionException>(() => e.SendAsync(new HttpRequestData("POST", cfg.Resolve("signup").AbsoluteUri, H(), Encoding.UTF8.GetBytes("{}")), CancellationToken.None)); await Assert.ThrowsAsync<HttpExecutionException>(() => e.SendAsync(new HttpRequestData("POST", cfg.Resolve("token?grant_type=refresh_token").AbsoluteUri, H(), Encoding.UTF8.GetBytes("{}")), CancellationToken.None)); }
        static string RetainedSecret(Store s) => Encoding.UTF8.GetString(s.Current.CopySecret()!);
        static void AssertPrincipal(AccountAuthLifecycleResult r) { Assert.NotNull(r.Principal); Assert.Equal(Subject.ToString("D"), r.Principal!.Subject); }
        [Fact] public async Task RecoveryRequiredIsTerminalWithoutAnExplicitRecoveryPath_UncertainRefreshKeepsPrincipalAndSecret() { var s = await Known(); var x = new Scripted(new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "test")); var r = await P(x, s).AuthenticateAsync(CancellationToken.None); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, r.Kind); AssertPrincipal(r); Assert.Single(x.Requests); Assert.Equal(SupabaseAnonymousSessionState.RecoveryRequired, s.Current.Public!.State); Assert.Equal(Subject.ToString("D"), s.Current.Public.Subject); Assert.NotNull(s.Current.CopySecret()); Assert.Equal("refresh1", RetainedSecret(s)); }
        [Fact] public async Task RecoveryRequiredIsTerminalWithoutAnExplicitRecoveryPath_ServerErrorRefreshKeepsPrincipalAndSecret() { var s = await Known(); var x = new Scripted(new HttpResponseData(500, new Dictionary<string, string> { { "Content-Type", "application/json" } }, Encoding.UTF8.GetBytes("{}"))); var r = await P(x, s).AuthenticateAsync(CancellationToken.None); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, r.Kind); AssertPrincipal(r); Assert.Single(x.Requests); Assert.Equal(SupabaseAnonymousSessionState.RecoveryRequired, s.Current.Public!.State); Assert.NotNull(s.Current.CopySecret()); Assert.Equal("refresh1", RetainedSecret(s)); }
        [Fact] public async Task RecoveryRequiredIsTerminalWithoutAnExplicitRecoveryPath_NotSentRefreshIsOfflineAndRestoresKnownWithSecret() { var s = await Known(); var x = new Scripted(new HttpExecutionException(HttpDeliveryCertainty.NotSent, "offline")); var r = await P(x, s).AuthenticateAsync(CancellationToken.None); Assert.Equal(AccountAuthLifecycleKind.UnavailableOffline, r.Kind); AssertPrincipal(r); Assert.Single(x.Requests); Assert.Equal(SupabaseAnonymousSessionState.Known, s.Current.Public!.State); Assert.Equal("refresh1", RetainedSecret(s)); var retry = new Scripted(Response("a2", "refresh2")); Assert.Equal(AccountAuthLifecycleKind.Authenticated, (await P(retry, s).AuthenticateAsync(CancellationToken.None)).Kind); Assert.Single(retry.Requests); }
        [Theory]
        [InlineData(SupabaseAnonymousSessionState.RecoveryRequired)]
        [InlineData(SupabaseAnonymousSessionState.RefreshPending)]
        public async Task RecoveryRequiredIsTerminalWithoutAnExplicitRecoveryPath_RetainedSecretIsNeverRetried(SupabaseAnonymousSessionState state) { var s = new Store(); await s.Force(state, Subject.ToString("D"), Encoding.UTF8.GetBytes("refresh1")); var version = s.Current.Public!.Version; var x = new Scripted(Response("a2", "refresh2")); var p = P(x, s); for (var i = 0; i < 2; i++) { var r = await p.AuthenticateAsync(CancellationToken.None); Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, r.Kind); AssertPrincipal(r); Assert.Null(r.Session); Assert.Empty(x.Requests); Assert.Equal(state, s.Current.Public!.State); Assert.Equal(version, s.Current.Public.Version); Assert.Equal("refresh1", RetainedSecret(s)); } }
        [Theory]
        [InlineData(SupabaseAnonymousSessionState.RecoveryRequired)]
        [InlineData(SupabaseAnonymousSessionState.RefreshPending)]
        [InlineData(SupabaseAnonymousSessionState.Known)]
        public async Task RecoverAsyncRotatesRetainedSecretAndPublishesSamePrincipal(SupabaseAnonymousSessionState state)
        {
            var store = await Forced(state, Subject.ToString("D"), "refresh1");
            var executor = new Scripted(Response("a2", "refresh2"));
            var provider = P(executor, store);

            var result = await provider.RecoverAsync(CancellationToken.None);

            Assert.Equal(AccountAuthLifecycleKind.Authenticated, result.Kind);
            AssertPrincipal(result);
            Assert.Equal("a2", (await result.Session!.GetAsync(CancellationToken.None)).Value);
            AssertOnlyRefreshRequests(executor, "refresh1");
            Assert.Equal(SupabaseAnonymousSessionState.Known, store.Current.Public!.State);
            Assert.Equal(Subject.ToString("D"), store.Current.Public.Subject);
            Assert.Equal("refresh2", RetainedSecret(store));
            var next = new Scripted(Response("a3", "refresh3"));
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, (await P(next, store).AuthenticateAsync(CancellationToken.None)).Kind);
            AssertOnlyRefreshRequests(next, "refresh2");
        }

        [Fact]
        public async Task RecoverAsyncAfterServerErrorRefreshRestoresKnownSessionWhileStartupStaysFailClosed()
        {
            var store = await Known();
            var failed = new Scripted(new HttpResponseData(500, Json(), Encoding.UTF8.GetBytes("{}")));
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(failed, store).AuthenticateAsync(CancellationToken.None)).Kind);
            var startup = new Scripted(Response("a2", "refresh2"));
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(startup, store).AuthenticateAsync(CancellationToken.None)).Kind);
            Assert.Empty(startup.Requests);

            var recovered = await P(startup, store).RecoverAsync(CancellationToken.None);

            Assert.Equal(AccountAuthLifecycleKind.Authenticated, recovered.Kind);
            AssertPrincipal(recovered);
            AssertOnlyRefreshRequests(startup, "refresh1");
            Assert.Equal(SupabaseAnonymousSessionState.Known, store.Current.Public!.State);
            Assert.Equal("refresh2", RetainedSecret(store));
        }

        [Fact]
        public async Task ConcurrentRecoverAsyncCallsShareOneRefreshRequest()
        {
            var store = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            var gate = new TaskCompletionSource<HttpResponseData>(TaskCreationOptions.RunContinuationsAsynchronously);
            var executor = new Scripted(gate.Task, Response("unused", "unused"));
            var provider = P(executor, store);

            var first = provider.RecoverAsync(CancellationToken.None);
            await executor.FirstSend.Task;
            var second = provider.RecoverAsync(CancellationToken.None);
            gate.SetResult(Response("a2", "refresh2"));

            Assert.Same(await first, await second);
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, (await first).Kind);
            AssertOnlyRefreshRequests(executor, "refresh1");
            Assert.Equal("refresh2", RetainedSecret(store));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(400)]
        [InlineData(401)]
        [InlineData(500)]
        public async Task RecoverAsyncUncertainOrRejectedRefreshKeepsRecoveryMarkerAndSecret(int status)
        {
            var store = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            var executor = status == 0
                ? new Scripted(new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "test"))
                : new Scripted(new HttpResponseData(status, Json(), Encoding.UTF8.GetBytes("{\"error\":\"invalid_grant\"}")));
            var provider = P(executor, store);

            var result = await provider.RecoverAsync(CancellationToken.None);

            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, result.Kind);
            AssertPrincipal(result);
            Assert.Null(result.Session);
            AssertOnlyRefreshRequests(executor, "refresh1");
            Assert.Equal(SupabaseAnonymousSessionState.RecoveryRequired, store.Current.Public!.State);
            Assert.Equal(Subject.ToString("D"), store.Current.Public.Subject);
            Assert.Equal("refresh1", RetainedSecret(store));
            var restart = new Scripted(Response("a2", "refresh2"));
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(restart, store).AuthenticateAsync(CancellationToken.None)).Kind);
            Assert.Empty(restart.Requests);
        }

        [Fact]
        public async Task RecoverAsyncRejectsSubjectMismatchWithoutReplacingPrincipal()
        {
            var store = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            var other = "{\"access_token\":\"a\",\"refresh_token\":\"r\",\"expires_at\":2000,\"user\":{\"id\":\"019a1234-2222-7777-8888-999999999999\"}}";
            var executor = new Scripted(new HttpResponseData(200, Json(), Encoding.UTF8.GetBytes(other)));

            var result = await P(executor, store).RecoverAsync(CancellationToken.None);

            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, result.Kind);
            AssertPrincipal(result);
            AssertOnlyRefreshRequests(executor, "refresh1");
            Assert.Equal(SupabaseAnonymousSessionState.RecoveryRequired, store.Current.Public!.State);
            Assert.Equal(Subject.ToString("D"), store.Current.Public.Subject);
            Assert.Equal("refresh1", RetainedSecret(store));
        }

        [Theory]
        [InlineData(SupabaseAnonymousSessionState.RecoveryRequired, true, "none")]
        [InlineData(SupabaseAnonymousSessionState.RecoveryRequired, true, "corrupt")]
        [InlineData(SupabaseAnonymousSessionState.RecoveryRequired, false, "valid")]
        [InlineData(SupabaseAnonymousSessionState.RefreshPending, true, "none")]
        [InlineData(SupabaseAnonymousSessionState.RefreshPending, true, "corrupt")]
        [InlineData(SupabaseAnonymousSessionState.Known, true, "corrupt")]
        [InlineData(SupabaseAnonymousSessionState.FreshAuthorized, false, "none")]
        [InlineData(SupabaseAnonymousSessionState.SignupPending, false, "none")]
        public async Task RecoverAsyncWithoutRecoverableCredentialSendsNothingAndKeepsMarker(SupabaseAnonymousSessionState state, bool hasSubject, string secret)
        {
            var bytes = secret == "none" ? null : secret == "corrupt" ? new byte[] { 0xff, 0xfe } : Encoding.UTF8.GetBytes("refresh1");
            var store = new Store();
            await store.Force(state, hasSubject ? Subject.ToString("D") : null, bytes);
            var version = store.Current.Public!.Version;
            var executor = new Scripted(Response("a2", "refresh2"));

            var result = await P(executor, store).RecoverAsync(CancellationToken.None);

            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, result.Kind);
            Assert.Null(result.Session);
            Assert.Empty(executor.Requests);
            Assert.Equal(state, store.Current.Public!.State);
            Assert.Equal(version, store.Current.Public.Version);
            Assert.Equal(bytes, store.Current.CopySecret());
        }

        [Fact]
        public async Task RecoverAsyncWithAbsentInvalidOrUnavailableStoreSendsNothing()
        {
            var executor = new Scripted(Response("a2", "refresh2"));
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(executor, new Store()).RecoverAsync(CancellationToken.None)).Kind);
            var overflow = new Store { Current = new SupabaseAnonymousSessionSnapshot(new SupabaseAnonymousSessionPublicRecord(long.MaxValue, SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D")), Encoding.UTF8.GetBytes("refresh1")) };
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(executor, overflow).RecoverAsync(CancellationToken.None)).Kind);
            var unavailable = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            unavailable.Available = false;
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(executor, unavailable).RecoverAsync(CancellationToken.None)).Kind);
            unavailable.Available = true;
            unavailable.NullRead = true;
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(executor, unavailable).RecoverAsync(CancellationToken.None)).Kind);
            Assert.Empty(executor.Requests);
            Assert.Equal("refresh1", RetainedSecret(unavailable));
        }

        [Theory]
        [InlineData(SupabaseAnonymousSessionState.RecoveryRequired)]
        [InlineData(SupabaseAnonymousSessionState.RefreshPending)]
        public async Task RecoverAsyncNotSentRestoresPriorMarkerAndIsOffline(SupabaseAnonymousSessionState state)
        {
            var store = await Forced(state, Subject.ToString("D"), "refresh1");
            var version = store.Current.Public!.Version;
            var executor = new Scripted(new HttpExecutionException(HttpDeliveryCertainty.NotSent, "offline"));

            var result = await P(executor, store).RecoverAsync(CancellationToken.None);

            Assert.Equal(AccountAuthLifecycleKind.UnavailableOffline, result.Kind);
            AssertPrincipal(result);
            AssertOnlyRefreshRequests(executor, "refresh1");
            Assert.Equal(state, store.Current.Public!.State);
            Assert.Equal(version + 2, store.Current.Public.Version);
            Assert.Equal("refresh1", RetainedSecret(store));
            var restart = new Scripted(Response("a2", "refresh2"));
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(restart, store).AuthenticateAsync(CancellationToken.None)).Kind);
            Assert.Empty(restart.Requests);
        }

        [Fact]
        public async Task RecoverAsyncCancellationBeforeCredentialPersistenceRetainsRecoverableMarker()
        {
            var store = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            using (var beforePending = new CancellationTokenSource())
            {
                store.Cancellation = beforePending;
                store.CancelOnCall = store.Calls + 1;
                var none = new Scripted(Response("a2", "refresh2"));
                Assert.Equal(AccountAuthLifecycleKind.Cancelled, (await P(none, store).RecoverAsync(beforePending.Token)).Kind);
                Assert.Empty(none.Requests);
                Assert.Equal(SupabaseAnonymousSessionState.RecoveryRequired, store.Current.Public!.State);
            }
            using var duringRequest = new CancellationTokenSource();
            var hung = new Scripted(new TaskCompletionSource<HttpResponseData>(TaskCreationOptions.RunContinuationsAsynchronously).Task);
            var pending = P(hung, store).RecoverAsync(duringRequest.Token);
            await hung.FirstSend.Task;
            duringRequest.Cancel();

            Assert.Equal(AccountAuthLifecycleKind.Cancelled, (await pending).Kind);
            Assert.Equal(SupabaseAnonymousSessionState.RefreshPending, store.Current.Public!.State);
            Assert.Equal(Subject.ToString("D"), store.Current.Public.Subject);
            Assert.Equal("refresh1", RetainedSecret(store));
            var retry = new Scripted(Response("a2", "refresh2"));
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, (await P(retry, store).RecoverAsync(CancellationToken.None)).Kind);
            AssertOnlyRefreshRequests(retry, "refresh1");
            Assert.Equal("refresh2", RetainedSecret(store));
        }

        [Fact]
        public async Task RecoverAsyncCancellationAfterCredentialPersistenceKeepsRotatedSecret()
        {
            var store = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            using var cancelled = new CancellationTokenSource();
            store.Cancellation = cancelled;
            store.CancelAfterApplyOnCall = store.Calls + 2;

            var result = await P(new Scripted(Response("a2", "refresh2")), store).RecoverAsync(cancelled.Token);

            Assert.True(cancelled.IsCancellationRequested);
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, result.Kind);
            Assert.Equal(SupabaseAnonymousSessionState.Known, store.Current.Public!.State);
            Assert.Equal("refresh2", RetainedSecret(store));
            var next = new Scripted(Response("a3", "refresh3"));
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, (await P(next, store).AuthenticateAsync(CancellationToken.None)).Kind);
            AssertOnlyRefreshRequests(next, "refresh2");
        }

        [Fact]
        public async Task RecoverAsyncStoreConflictPublishesNoSession()
        {
            var store = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            var version = store.Current.Public!.Version;
            store.RejectOnCall = store.Calls + 1;
            var claimLost = new Scripted(Response("a2", "refresh2"));
            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, (await P(claimLost, store).RecoverAsync(CancellationToken.None)).Kind);
            Assert.Empty(claimLost.Requests);
            Assert.Equal(version, store.Current.Public!.Version);

            store.RejectKnown = true;
            var publishLost = new Scripted(Response("a2", "refresh2"));
            var result = await P(publishLost, store).RecoverAsync(CancellationToken.None);

            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, result.Kind);
            Assert.Null(result.Session);
            AssertOnlyRefreshRequests(publishLost, "refresh1");
            Assert.NotEqual(SupabaseAnonymousSessionState.Known, store.Current.Public!.State);
            Assert.Equal("refresh1", RetainedSecret(store));
        }

        [Fact]
        public async Task RecoverAsyncTimeoutBoundsAHungRefreshAndKeepsRecoverableMarker()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => C(recoveryTimeout: TimeSpan.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => C(recoveryTimeout: TimeSpan.FromMinutes(6)));
            var store = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            var hung = new Scripted(new TaskCompletionSource<HttpResponseData>(TaskCreationOptions.RunContinuationsAsynchronously).Task);
            var provider = new SupabaseAnonymousAuthLifecycle(C(recoveryTimeout: TimeSpan.FromMilliseconds(50)), hung, store, new Clock());

            var result = await provider.RecoverAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(AccountAuthLifecycleKind.RecoveryRequired, result.Kind);
            Assert.Null(result.Session);
            Assert.Single(hung.Requests);
            Assert.Equal(SupabaseAnonymousSessionState.RefreshPending, store.Current.Public!.State);
            Assert.Equal("refresh1", RetainedSecret(store));
        }

        [Fact]
        public async Task RecoverAsyncPersistsRotatedSecretWhenCallerCancelsAfterTheResponse()
        {
            var store = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            using var cancelled = new CancellationTokenSource();
            store.Cancellation = cancelled;
            store.CancelOnCall = store.Calls + 2;

            var result = await P(new Scripted(Response("a2", "refresh2")), store).RecoverAsync(cancelled.Token);

            Assert.True(cancelled.IsCancellationRequested);
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, result.Kind);
            AssertPrincipal(result);
            Assert.Equal(SupabaseAnonymousSessionState.Known, store.Current.Public!.State);
            Assert.Equal("refresh2", RetainedSecret(store));
        }

        [Fact]
        public async Task RecoverAsyncPersistsRotatedSecretWhenTheTimeoutFiresAfterTheResponse()
        {
            var store = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            store.HonourCancellation = true;
            Func<CancellationToken, Task<HttpResponseData>> lateResponse = async t =>
            {
                try { await Task.Delay(Timeout.Infinite, t); } catch (OperationCanceledException) { }
                return Response("a2", "refresh2");
            };
            var executor = new Scripted(lateResponse);
            var provider = new SupabaseAnonymousAuthLifecycle(C(recoveryTimeout: TimeSpan.FromMilliseconds(50)), executor, store, new Clock());

            var result = await provider.RecoverAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(AccountAuthLifecycleKind.Authenticated, result.Kind);
            AssertOnlyRefreshRequests(executor, "refresh1");
            Assert.Equal(SupabaseAnonymousSessionState.Known, store.Current.Public!.State);
            Assert.Equal("refresh2", RetainedSecret(store));
        }

        [Fact]
        public async Task StartupRefreshStillHonoursCancellationBeforeTheKnownWrite()
        {
            var store = await Known();
            using var cancelled = new CancellationTokenSource();
            store.Cancellation = cancelled;
            store.CancelOnCall = store.Calls + 2;

            var result = await P(new Scripted(Response("a2", "refresh2")), store).AuthenticateAsync(cancelled.Token);

            Assert.Equal(AccountAuthLifecycleKind.Cancelled, result.Kind);
            Assert.Equal(SupabaseAnonymousSessionState.RefreshPending, store.Current.Public!.State);
        }

        [Fact]
        public async Task CancelledJoinerReturnsCancelledWhileOwnerCompletesWithOneRequest()
        {
            var store = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            var gate = new TaskCompletionSource<HttpResponseData>(TaskCreationOptions.RunContinuationsAsynchronously);
            var executor = new Scripted(gate.Task, Response("unused", "unused"));
            var provider = P(executor, store);
            var owner = provider.RecoverAsync(CancellationToken.None);
            await executor.FirstSend.Task;
            using var cancelled = new CancellationTokenSource();
            var joiner = provider.RecoverAsync(cancelled.Token);

            cancelled.Cancel();

            Assert.Equal(AccountAuthLifecycleKind.Cancelled, (await joiner).Kind);
            Assert.False(owner.IsCompleted);
            gate.SetResult(Response("a2", "refresh2"));
            Assert.Equal(AccountAuthLifecycleKind.Authenticated, (await owner).Kind);
            AssertOnlyRefreshRequests(executor, "refresh1");
            Assert.Equal("refresh2", RetainedSecret(store));
        }

        [Fact]
        public async Task FaultedRecoveryAttemptIsNotCached()
        {
            var store = await Forced(SupabaseAnonymousSessionState.RecoveryRequired, Subject.ToString("D"), "refresh1");
            var executor = new Scripted(Response("a2", "refresh2"));
            var provider = P(executor, store);
            store.ThrowOnAvailable = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.RecoverAsync(CancellationToken.None));
            Assert.Empty(executor.Requests);
            store.ThrowOnAvailable = false;

            var result = await provider.RecoverAsync(CancellationToken.None);

            Assert.Equal(AccountAuthLifecycleKind.Authenticated, result.Kind);
            AssertOnlyRefreshRequests(executor, "refresh1");
        }

        static Dictionary<string, string> Json() => new Dictionary<string, string> { { "Content-Type", "application/json" } };
        static async Task<Store> Forced(SupabaseAnonymousSessionState state, string? subject, string refresh)
        {
            var store = new Store();
            await store.Force(state, subject, Encoding.UTF8.GetBytes(refresh));
            return store;
        }
        static void AssertOnlyRefreshRequests(Scripted executor, string refresh)
        {
            Assert.Single(executor.Requests);
            Assert.EndsWith("/auth/v1/token?grant_type=refresh_token", executor.Requests[0].Uri, StringComparison.Ordinal);
            Assert.Equal("{\"refresh_token\":\"" + refresh + "\"}", Encoding.UTF8.GetString(executor.Requests[0].CopyBody()));
        }
        static SupabaseAnonymousAuthLifecycle P(IHttpExecutor e, Store s) => new SupabaseAnonymousAuthLifecycle(C(), e, s, new Clock()); static SupabaseAuthConfiguration C(int n = 65536, TimeSpan? recoveryTimeout = null) => new SupabaseAuthConfiguration(new Uri("https://auth.example.test/auth/v1/"), "https://auth.example.test/auth/v1", new BackendNamespace("test"), "public-test", "platform.supabase.session", n, recoveryTimeout); static IReadOnlyDictionary<string, string> H() => new Dictionary<string, string> { { "apikey", "public-test" }, { "Accept", "application/json" }, { "Content-Type", "application/json" } }; static HttpResponseData Response(string a = "access", string r = "refresh", long expires = 2000) => new HttpResponseData(200, new Dictionary<string, string> { { "Content-Type", "application/json" } }, Encoding.UTF8.GetBytes("{\"access_token\":\"" + a + "\",\"refresh_token\":\"" + r + "\",\"expires_at\":" + expires + ",\"user\":{\"id\":\"" + Subject.ToString("D") + "\"}}")); static async Task<Store> Known() { var s = new Store(); var p = P(new Scripted(Response("a", "refresh1")), s); Assert.True(await p.AuthorizeFreshAsync(CancellationToken.None)); await p.AuthenticateAsync(CancellationToken.None); return s; }
        sealed class Clock : IUnixMillisecondClock { public long GetUnixMilliseconds() => 1000000; }
        sealed class Store : ISupabaseAnonymousSessionStore { public bool Available = true, NullRead, RejectKnown, ReturnMaxOnSuccess, HonourCancellation, ThrowOnAvailable; public int RejectOnCall, ThrowOnCall, CancelOnCall, CancelAfterApplyOnCall, Calls; public CancellationTokenSource? Cancellation; public readonly List<SupabaseAnonymousSessionSnapshot> Writes = new List<SupabaseAnonymousSessionSnapshot>(); public SupabaseAnonymousSessionSnapshot Current = SupabaseAnonymousSessionSnapshot.Absent(); public bool IsAvailable => ThrowOnAvailable ? throw new InvalidOperationException("store failure") : Available; public Task<SupabaseAnonymousSessionSnapshot> ReadAsync(string k, CancellationToken t) => Task.FromResult(NullRead ? null! : Clone(Current)); public Task<SupabaseAnonymousSessionCompareExchangeResult> CompareExchangeAsync(string k, SupabaseAnonymousSessionSnapshot expected, SupabaseAnonymousSessionTransition next, byte[]? secret, CancellationToken t) { var call = Interlocked.Increment(ref Calls); if (call == CancelOnCall) Cancellation!.Cancel(); if ((call == CancelOnCall || HonourCancellation) && t.IsCancellationRequested) return Task.FromCanceled<SupabaseAnonymousSessionCompareExchangeResult>(t); if (call == ThrowOnCall) throw new InvalidOperationException("store failure"); var match = expected.Exists == Current.Exists && (!expected.Exists || (expected.Public!.Version == Current.Public!.Version && expected.Public.State == Current.Public.State)); if ((RejectKnown && next.State == SupabaseAnonymousSessionState.Known) || call == RejectOnCall) match = false; if (!match) return Task.FromResult(new SupabaseAnonymousSessionCompareExchangeResult(false, Clone(Current))); Current = new SupabaseAnonymousSessionSnapshot(new SupabaseAnonymousSessionPublicRecord(Current.Exists ? Current.Public!.Version + 1 : 1, next.State, next.Subject), secret); Writes.Add(Clone(Current)); if (call == CancelAfterApplyOnCall) Cancellation!.Cancel(); var result = ReturnMaxOnSuccess ? new SupabaseAnonymousSessionSnapshot(new SupabaseAnonymousSessionPublicRecord(long.MaxValue, next.State, next.Subject), secret) : Clone(Current); return Task.FromResult(new SupabaseAnonymousSessionCompareExchangeResult(true, result)); } public async Task Force(SupabaseAnonymousSessionState state, string? subject, byte[]? secret) { var e = await ReadAsync("x", CancellationToken.None); await CompareExchangeAsync("x", e, new SupabaseAnonymousSessionTransition(state, subject), secret, CancellationToken.None); } static SupabaseAnonymousSessionSnapshot Clone(SupabaseAnonymousSessionSnapshot v) => new SupabaseAnonymousSessionSnapshot(v.Public == null ? null : new SupabaseAnonymousSessionPublicRecord(v.Public.Version, v.Public.State, v.Public.Subject), v.CopySecret()); }
        sealed class Scripted : IHttpExecutor { readonly Queue<object> q = new Queue<object>(); public List<HttpRequestData> Requests = new List<HttpRequestData>(); public TaskCompletionSource<bool> FirstSend = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); public Scripted(params object[] x) { foreach (var a in x) q.Enqueue(a); } public async Task<HttpResponseData> SendAsync(HttpRequestData r, CancellationToken t) { Requests.Add(r); FirstSend.TrySetResult(true); if (q.Count == 0) throw new HttpExecutionException(HttpDeliveryCertainty.Uncertain, "test"); var x = q.Dequeue(); if (x is Exception e) throw e; if (x is Task<HttpResponseData> task) return await task.WaitAsync(t).ConfigureAwait(false); if (x is Func<CancellationToken, Task<HttpResponseData>> respond) return await respond(t).ConfigureAwait(false); return (HttpResponseData)x; } }
        sealed class Handler : HttpMessageHandler { readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> f; public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> f) { this.f = f; } protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t) => f(r, t); }
    }
}
