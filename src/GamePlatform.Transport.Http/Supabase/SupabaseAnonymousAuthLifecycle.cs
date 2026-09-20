#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Accounts;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Transport.Abstractions;

namespace GamePlatform.Transport.Http.Supabase
{
    /// <summary>CAS-fenced nonproduction Supabase anonymous-auth adapter.</summary>
    public sealed class SupabaseAnonymousAuthLifecycle : IAccountsAuthLifecycle
    {
        private const string JsonMediaType = "application/json";
        private const string SignupBody = "{\"data\":{}}";
        private readonly SupabaseAuthConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly ISupabaseAnonymousSessionStore store;
        private readonly IUnixMillisecondClock clock;
        private readonly SemaphoreSlim authenticateGate = new SemaphoreSlim(1, 1);

        public SupabaseAnonymousAuthLifecycle(SupabaseAuthConfiguration configuration, IHttpExecutor executor,
            ISupabaseAnonymousSessionStore store, IUnixMillisecondClock? clock = null)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.clock = clock ?? new SystemUnixMillisecondClock();
        }

        /// <summary>
        /// Explicit host-only fresh authorization. The host must first decide that no
        /// account directory or recovery data exists; this class never scans it.
        /// Authentication itself treats an absent marker as recovery.
        /// </summary>
        public async Task<bool> AuthorizeFreshAsync(CancellationToken cancellationToken)
        {
            if (!store.IsAvailable) return false;
            try
            {
                var current = await store.ReadAsync(configuration.SessionStorageKey, cancellationToken).ConfigureAwait(false);
                if (current == null || current.Exists) return false;
                var result = await store.CompareExchangeAsync(configuration.SessionStorageKey, current,
                    new SupabaseAnonymousSessionTransition(SupabaseAnonymousSessionState.FreshAuthorized, null), null, cancellationToken).ConfigureAwait(false);
                return result.Applied && ValidSnapshot(result.Current);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
            catch { return false; }
        }

        public async Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken cancellationToken)
        {
            try { await authenticateGate.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return AccountAuthLifecycleResult.Cancelled(); }
            try
            {
                if (!store.IsAvailable) return AccountAuthLifecycleResult.RecoveryRequired(null);
                SupabaseAnonymousSessionSnapshot current;
                try { current = await store.ReadAsync(configuration.SessionStorageKey, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return AccountAuthLifecycleResult.Cancelled(); }
                catch { return AccountAuthLifecycleResult.RecoveryRequired(null); }
                if (!ValidSnapshot(current)) return AccountAuthLifecycleResult.RecoveryRequired(null);
                switch (current.Public!.State)
                {
                    case SupabaseAnonymousSessionState.FreshAuthorized: return await BeginSignupAsync(current, cancellationToken).ConfigureAwait(false);
                    case SupabaseAnonymousSessionState.Known: return await BeginRefreshAsync(current, cancellationToken).ConfigureAwait(false);
                    case SupabaseAnonymousSessionState.SignupPending:
                    case SupabaseAnonymousSessionState.RefreshPending:
                    case SupabaseAnonymousSessionState.RecoveryRequired:
                    default: return AccountAuthLifecycleResult.RecoveryRequired(Principal(current.Public.Subject));
                }
            }
            finally { authenticateGate.Release(); }
        }

        private async Task<AccountAuthLifecycleResult> BeginSignupAsync(SupabaseAnonymousSessionSnapshot current, CancellationToken cancellationToken)
        {
            var pending = await TransitionAsync(current, SupabaseAnonymousSessionState.SignupPending, null, null, cancellationToken).ConfigureAwait(false);
            if (pending.Cancelled) return AccountAuthLifecycleResult.Cancelled();
            if (!pending.Applied || pending.Current == null) return AccountAuthLifecycleResult.RecoveryRequired(null);
            var result = await SendAsync("signup", SignupBody, cancellationToken).ConfigureAwait(false);
            if (result.Cancelled) return AccountAuthLifecycleResult.Cancelled();
            if (result.Certainty == HttpDeliveryCertainty.NotSent)
            {
                var restored = await TransitionAsync(pending.Current, SupabaseAnonymousSessionState.FreshAuthorized, null, null, cancellationToken).ConfigureAwait(false);
                if (restored.Cancelled) return AccountAuthLifecycleResult.Cancelled();
                return restored.Applied ? AccountAuthLifecycleResult.UnavailableOffline(null) : AccountAuthLifecycleResult.RecoveryRequired(null);
            }
            if (result.Certainty == HttpDeliveryCertainty.Uncertain || result.Response == null || !IsSuccess(result.Response) || !TryDecode(result.Response, out var payload)) { await RecoverAsync(pending.Current, null, cancellationToken).ConfigureAwait(false); return AccountAuthLifecycleResult.RecoveryRequired(null); }
            var known = await TransitionAsync(pending.Current, SupabaseAnonymousSessionState.Known, payload.Subject, EncodeRefresh(payload.RefreshToken), cancellationToken).ConfigureAwait(false);
            if (known.Cancelled) return AccountAuthLifecycleResult.Cancelled();
            if (!known.Applied || !ValidSnapshot(known.Current)) return AccountAuthLifecycleResult.RecoveryRequired(null);
            return Authenticated(known.Current!, payload);
        }

        private async Task<AccountAuthLifecycleResult> BeginRefreshAsync(SupabaseAnonymousSessionSnapshot current, CancellationToken cancellationToken)
        {
            var subject = current.Public!.Subject; var refresh = DecodeRefresh(current.CopySecret());
            if (subject == null || refresh == null) return AccountAuthLifecycleResult.RecoveryRequired(Principal(subject));
            var pending = await TransitionAsync(current, SupabaseAnonymousSessionState.RefreshPending, subject, EncodeRefresh(refresh), cancellationToken).ConfigureAwait(false);
            if (pending.Cancelled) return AccountAuthLifecycleResult.Cancelled();
            if (!pending.Applied || pending.Current == null) return AccountAuthLifecycleResult.RecoveryRequired(Principal(subject));
            return await RefreshPendingAsync(pending.Current, subject, refresh, cancellationToken).ConfigureAwait(false);
        }

        private async Task<AccountAuthLifecycleResult> RefreshPendingAsync(SupabaseAnonymousSessionSnapshot pending, string subject, string refresh, CancellationToken cancellationToken)
        {
            var result = await SendAsync("token?grant_type=refresh_token", RefreshBody(refresh), cancellationToken).ConfigureAwait(false);
            if (result.Cancelled) return AccountAuthLifecycleResult.Cancelled();
            if (result.Certainty == HttpDeliveryCertainty.NotSent)
            {
                var restored = await TransitionAsync(pending, SupabaseAnonymousSessionState.Known, subject, EncodeRefresh(refresh), cancellationToken).ConfigureAwait(false);
                if (restored.Cancelled) return AccountAuthLifecycleResult.Cancelled();
                return restored.Applied ? AccountAuthLifecycleResult.UnavailableOffline(Principal(subject)) : AccountAuthLifecycleResult.RecoveryRequired(Principal(subject));
            }
            if (result.Certainty == HttpDeliveryCertainty.Uncertain || result.Response == null || !IsSuccess(result.Response) || !TryDecode(result.Response, out var payload) || !string.Equals(subject, payload.Subject, StringComparison.Ordinal)) { await RecoverAsync(pending, subject, cancellationToken).ConfigureAwait(false); return AccountAuthLifecycleResult.RecoveryRequired(Principal(subject)); }
            var known = await TransitionAsync(pending, SupabaseAnonymousSessionState.Known, subject, EncodeRefresh(payload.RefreshToken), cancellationToken).ConfigureAwait(false);
            if (known.Cancelled) return AccountAuthLifecycleResult.Cancelled();
            return known.Applied && ValidSnapshot(known.Current) ? Authenticated(known.Current!, payload) : AccountAuthLifecycleResult.RecoveryRequired(Principal(subject));
        }

        private AccountAuthLifecycleResult Authenticated(SupabaseAnonymousSessionSnapshot current, AuthPayload payload)
        {
            var principal = Principal(current.Public?.Subject); if (principal == null) throw new InvalidOperationException("A durable known session requires a principal.");
            return AccountAuthLifecycleResult.Authenticated(principal, new SupabaseAuthSession(this, current, payload));
        }

        private async Task<TransitionResult> TransitionAsync(SupabaseAnonymousSessionSnapshot expected, SupabaseAnonymousSessionState state, string? subject, byte[]? secret, CancellationToken cancellationToken)
        {
            if (expected == null || (expected.Exists && !ValidSnapshot(expected))) return new TransitionResult(false, null, false);
            try
            {
                var result = await store.CompareExchangeAsync(configuration.SessionStorageKey, expected, new SupabaseAnonymousSessionTransition(state, subject), secret, cancellationToken).ConfigureAwait(false);
                return result.Applied && !ValidSnapshot(result.Current)
                    ? new TransitionResult(false, result.Current, false)
                    : new TransitionResult(result.Applied, result.Current, false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new TransitionResult(false, null, true); }
            catch { return new TransitionResult(false, null, false); }
        }
        private async Task RecoverAsync(SupabaseAnonymousSessionSnapshot expected, string? subject, CancellationToken token) => await TransitionAsync(expected, SupabaseAnonymousSessionState.RecoveryRequired, subject, expected.CopySecret(), token).ConfigureAwait(false);
        private static bool ValidSnapshot(SupabaseAnonymousSessionSnapshot? value) => value != null && value.Exists && value.Public != null && value.Public.Version > 0 && value.Public.Version < long.MaxValue;
        private AccountPrincipalDescriptor? Principal(string? subject) => subject == null ? null : new AccountPrincipalDescriptor(configuration.BackendNamespace, configuration.PrincipalIssuer, subject);

        private async Task<SendResult> SendAsync(string path, string json, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["apikey"] = configuration.PublishableApiKey,
                ["Accept"] = JsonMediaType,
                ["Content-Type"] = JsonMediaType
            };
            try
            {
                var response = await executor.SendAsync(new HttpRequestData("POST", configuration.Resolve(path).AbsoluteUri, headers, bytes), cancellationToken).ConfigureAwait(false);
                return SendResult.ResponseResult(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return SendResult.CancelledResult(); }
            catch (HttpExecutionException error) { return SendResult.Failed(error.Certainty); }
            catch { return SendResult.Failed(HttpDeliveryCertainty.Uncertain); }
        }

        private bool TryDecode(HttpResponseData response, out AuthPayload payload)
        {
            payload = null!;
            if (response.BodyLength == 0 || response.BodyLength > configuration.MaximumResponseBytes ||
                !response.TryGetHeader("Content-Type", out var contentType) || !IsJsonMediaType(contentType) ||
                (response.TryGetHeader("Content-Encoding", out var encoding) && !string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase))) return false;
            try { return AuthPayload.TryDecode(response.CopyBody(), clock.GetUnixMilliseconds(), out payload); }
            catch { return false; }
        }

        private static bool IsSuccess(HttpResponseData response) => response.StatusCode == 200 || response.StatusCode == 201;
        private static bool IsJsonMediaType(string value)
        {
            var separator = value.IndexOf(';');
            return string.Equals((separator < 0 ? value : value.Substring(0, separator)).Trim(), JsonMediaType, StringComparison.OrdinalIgnoreCase);
        }
        private static string RefreshBody(string refreshToken) => "{\"refresh_token\":" + JsonString(refreshToken) + "}";
        private static string JsonString(string value)
        {
            var output = new StringBuilder(value.Length + 2); output.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"': output.Append("\\\""); break; case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break; case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break; case '\r': output.Append("\\r"); break; case '\t': output.Append("\\t"); break;
                    default: if (character < 32) output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)); else output.Append(character); break;
                }
            }
            return output.Append('"').ToString();
        }

        private static byte[] EncodeRefresh(string value) => Encoding.UTF8.GetBytes(value);
        private static string? DecodeRefresh(byte[]? value)
        {
            try { var token = value == null ? null : new UTF8Encoding(false, true).GetString(value); return ValidToken(token) ? token : null; } catch { return null; }
        }

        private sealed class SupabaseAuthSession : IAuthSession
        {
            private readonly SupabaseAnonymousAuthLifecycle owner;
            private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
            private SupabaseAnonymousSessionSnapshot record;
            private AuthPayload payload;
            private long generation;

            internal SupabaseAuthSession(SupabaseAnonymousAuthLifecycle owner, SupabaseAnonymousSessionSnapshot record, AuthPayload payload)
            {
                this.owner = owner; this.record = record; this.payload = payload;
            }

            public string SessionKey => owner.configuration.BackendNamespace.Value + ":" + record.Public!.Subject;

            public async Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken)
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (payload.ExpiresAtMilliseconds > owner.clock.GetUnixMilliseconds()) return Snapshot();
                    return await RefreshLockedAsync(generation, cancellationToken).ConfigureAwait(false);
                }
                finally { gate.Release(); }
            }

            public async Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken)
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try { return rejectedGeneration == generation ? await RefreshLockedAsync(rejectedGeneration, cancellationToken).ConfigureAwait(false) : Snapshot(); }
                finally { gate.Release(); }
            }

            private async Task<AccessTokenSnapshot> RefreshLockedAsync(long expectedGeneration, CancellationToken cancellationToken)
            {
                if (expectedGeneration != generation) return Snapshot();
                var subject = record.Public!.Subject!; var refresh = DecodeRefresh(record.CopySecret());
                if (refresh == null) throw new InvalidOperationException("The Supabase Auth session requires recovery.");
                var pending = await owner.TransitionAsync(record, SupabaseAnonymousSessionState.RefreshPending, subject, EncodeRefresh(refresh), cancellationToken).ConfigureAwait(false);
                if (pending.Cancelled) throw new OperationCanceledException(cancellationToken);
                if (!pending.Applied || pending.Current == null) throw new InvalidOperationException("The Supabase Auth session requires recovery.");
                var result = await owner.SendAsync("token?grant_type=refresh_token", RefreshBody(refresh), cancellationToken).ConfigureAwait(false);
                if (result.Cancelled) throw new OperationCanceledException(cancellationToken);
                if (result.Certainty == HttpDeliveryCertainty.NotSent) { var restored = await owner.TransitionAsync(pending.Current, SupabaseAnonymousSessionState.Known, subject, EncodeRefresh(refresh), cancellationToken).ConfigureAwait(false); if (restored.Cancelled) throw new OperationCanceledException(cancellationToken); if (!restored.Applied) throw new InvalidOperationException("The Supabase Auth session requires recovery."); throw new InvalidOperationException("The Supabase Auth session is temporarily unavailable."); }
                if (result.Response == null || !IsSuccess(result.Response) || !owner.TryDecode(result.Response, out var next) || !string.Equals(next.Subject, subject, StringComparison.Ordinal))
                    throw new InvalidOperationException("The Supabase Auth session requires recovery or is temporarily unavailable.");
                var known = await owner.TransitionAsync(pending.Current, SupabaseAnonymousSessionState.Known, subject, EncodeRefresh(next.RefreshToken), cancellationToken).ConfigureAwait(false);
                if (!known.Applied || !ValidSnapshot(known.Current)) throw new InvalidOperationException("The Supabase Auth session requires recovery.");
                record = known.Current!; payload = next; generation = checked(generation + 1); return Snapshot();
            }

            private AccessTokenSnapshot Snapshot() => new AccessTokenSnapshot(payload.AccessToken, generation);
        }

        private sealed class TransitionResult { public TransitionResult(bool applied, SupabaseAnonymousSessionSnapshot? current, bool cancelled) { Applied = applied; Current = current; Cancelled = cancelled; } public bool Applied { get; } public SupabaseAnonymousSessionSnapshot? Current { get; } public bool Cancelled { get; } }
        private sealed class SendResult
        {
            private SendResult(HttpResponseData? response, HttpDeliveryCertainty? certainty, bool cancelled) { Response = response; Certainty = certainty; Cancelled = cancelled; }
            public HttpResponseData? Response { get; } public HttpDeliveryCertainty? Certainty { get; } public bool Cancelled { get; }
            public static SendResult ResponseResult(HttpResponseData response) => new SendResult(response, null, false);
            public static SendResult Failed(HttpDeliveryCertainty certainty) => new SendResult(null, certainty, false);
            public static SendResult CancelledResult() => new SendResult(null, null, true);
        }

        private sealed class AuthPayload
        {
            private AuthPayload(string accessToken, string refreshToken, string subject, long expiresAtMilliseconds) { AccessToken = accessToken; RefreshToken = refreshToken; Subject = subject; ExpiresAtMilliseconds = expiresAtMilliseconds; }
            public string AccessToken { get; } public string RefreshToken { get; } public string Subject { get; } public long ExpiresAtMilliseconds { get; }
            public static bool TryDecode(byte[] body, long nowMilliseconds, out AuthPayload payload)
            {
                payload = null!;
                if (nowMilliseconds < 0 || nowMilliseconds > PlatformNumbers.MaximumUnixMilliseconds || body == null || body.Length == 0 || body.Length > 65_536) return false;
                string json;
                try { json = new UTF8Encoding(false, true).GetString(body); } catch { return false; }
                var reader = new BoundedJsonReader(json); if (!reader.TryReadAuth(out var access, out var refresh, out var subject, out var expiresAtSeconds, out var expiresIn)) return false;
                if (!ValidToken(access) || !ValidToken(refresh) || !Guid.TryParseExact(subject, "D", out var id) || id == Guid.Empty) return false;
                long expires;
                try
                {
                    expires = expiresAtSeconds.HasValue ? checked(expiresAtSeconds.Value * 1_000L) : checked(nowMilliseconds + checked(expiresIn!.Value * 1_000L));
                    PlatformNumbers.UnixMilliseconds(expires);
                }
                catch { return false; }
                payload = new AuthPayload(access!, refresh!, id.ToString("D"), expires); return true;
            }
        }

        private static bool ValidToken(string? value) => !string.IsNullOrWhiteSpace(value) && value!.Length <= 16_384 && value.IndexOfAny(new[] { '\r', '\n', '\0' }) < 0;

        /// <summary>Minimal strict JSON reader: it retains only the fields GoTrue returns and rejects duplicate target fields.</summary>
        private sealed class BoundedJsonReader
        {
            private readonly string text; private int index;
            internal BoundedJsonReader(string text) { this.text = text ?? string.Empty; }
            internal bool TryReadAuth(out string? access, out string? refresh, out string? subject, out long? expiresAt, out long? expiresIn)
            {
                access = refresh = subject = null; expiresAt = expiresIn = null;
                try
                {
                    White(); Expect('{'); var seenAccess = false; var seenRefresh = false; var seenUser = false; var seenExpiresAt = false; var seenExpiresIn = false;
                    if (Try('}')) return false;
                    while (true)
                    {
                        var key = String(); White(); Expect(':'); White();
                        if (key == "access_token") { if (seenAccess) return false; seenAccess = true; access = String(); }
                        else if (key == "refresh_token") { if (seenRefresh) return false; seenRefresh = true; refresh = String(); }
                        else if (key == "expires_at") { if (seenExpiresAt) return false; seenExpiresAt = true; expiresAt = Integer(); }
                        else if (key == "expires_in") { if (seenExpiresIn) return false; seenExpiresIn = true; expiresIn = Integer(); }
                        else if (key == "user") { if (seenUser) return false; seenUser = true; subject = User(); }
                        else Skip(0);
                        White(); if (Try('}')) break; Expect(','); White();
                    }
                    White(); return index == text.Length && access != null && refresh != null && subject != null && (expiresAt.HasValue || expiresIn.HasValue) && (!expiresAt.HasValue || expiresAt.Value > 0) && (!expiresIn.HasValue || expiresIn.Value > 0);
                }
                catch { return false; }
            }
            private string User()
            {
                White(); Expect('{'); string? id = null; var seen = false; if (Try('}')) throw new FormatException();
                while (true) { var key = String(); White(); Expect(':'); White(); if (key == "id") { if (seen) throw new FormatException(); seen = true; id = String(); } else Skip(1); White(); if (Try('}')) break; Expect(','); White(); }
                return id ?? throw new FormatException();
            }
            private void Skip(int depth)
            {
                if (depth > 16) throw new FormatException(); White(); if (index >= text.Length) throw new FormatException();
                if (text[index] == '"') { String(); return; }
                if (text[index] == '{') { index++; White(); if (Try('}')) return; while (true) { String(); White(); Expect(':'); Skip(depth + 1); White(); if (Try('}')) return; Expect(','); White(); } }
                if (text[index] == '[') { index++; White(); if (Try(']')) return; while (true) { Skip(depth + 1); White(); if (Try(']')) return; Expect(','); White(); } }
                if (Starts("true")) { index += 4; return; } if (Starts("false")) { index += 5; return; } if (Starts("null")) { index += 4; return; } Number();
            }
            private long Integer() { var value = Number(); if (value.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0 || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)) throw new FormatException(); return result; }
            private string Number()
            {
                var start = index; if (Try('-')) { } if (index >= text.Length) throw new FormatException(); if (text[index] == '0') index++; else { Digit19(); while (index < text.Length && Digit(text[index])) index++; }
                if (Try('.')) { Digit0(); while (index < text.Length && Digit(text[index])) index++; } if (index < text.Length && (text[index] == 'e' || text[index] == 'E')) { index++; if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++; Digit0(); while (index < text.Length && Digit(text[index])) index++; }
                return text.Substring(start, index - start);
            }
            private string String()
            {
                Expect('"'); var result = new StringBuilder(); while (index < text.Length)
                {
                    var c = text[index++]; if (c == '"') return result.ToString(); if (c < 32) throw new FormatException();
                    if (c != '\\') { result.Append(c); if (result.Length > 16_384) throw new FormatException(); continue; }
                    if (index >= text.Length) throw new FormatException(); var escape = text[index++];
                    switch (escape) { case '"': result.Append('"'); break; case '\\': result.Append('\\'); break; case '/': result.Append('/'); break; case 'b': result.Append('\b'); break; case 'f': result.Append('\f'); break; case 'n': result.Append('\n'); break; case 'r': result.Append('\r'); break; case 't': result.Append('\t'); break; case 'u': result.Append((char)Hex4()); break; default: throw new FormatException(); }
                    if (result.Length > 16_384) throw new FormatException();
                }
                throw new FormatException();
            }
            private int Hex4() { if (index + 4 > text.Length) throw new FormatException(); var result = 0; for (var i = 0; i < 4; i++) { var c = text[index++]; result = checked(result * 16 + (c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : throw new FormatException())); } return result; }
            private bool Starts(string value) => index + value.Length <= text.Length && string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
            private void White() { while (index < text.Length && (text[index] == ' ' || text[index] == '\t' || text[index] == '\r' || text[index] == '\n')) index++; }
            private bool Try(char value) { if (index < text.Length && text[index] == value) { index++; return true; } return false; }
            private void Expect(char value) { if (!Try(value)) throw new FormatException(); }
            private void Digit19() { if (index >= text.Length || text[index] < '1' || text[index] > '9') throw new FormatException(); index++; }
            private void Digit0() { if (index >= text.Length || !Digit(text[index])) throw new FormatException(); index++; }
            private static bool Digit(char value) => value >= '0' && value <= '9';
        }
    }
}
