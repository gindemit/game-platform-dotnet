#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
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
    /// <summary>
    /// Bounded nonproduction Supabase anonymous-auth adapter. It speaks only the
    /// vendor's explicit JSON auth protocol, never interprets JWT claims, and
    /// maps the returned user id to the configured non-secret principal issuer.
    /// Access tokens are memory-only; the injected secure store receives just a
    /// framed refresh token and subject record.
    /// </summary>
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

        /// <summary>Convenience composition for desktop/non-Unity hosts. The caller owns HttpClient and disables redirects on its handler.</summary>
        public static SupabaseAnonymousAuthLifecycle CreateNonProduction(SupabaseAuthConfiguration configuration,
            HttpClient client, ISupabaseAnonymousSessionStore store, IUnixMillisecondClock? clock = null) =>
            new SupabaseAnonymousAuthLifecycle(configuration, new BoundedSupabaseAuthHttpExecutor(client, configuration), store, clock);

        public async Task<AccountAuthLifecycleResult> AuthenticateAsync(CancellationToken cancellationToken)
        {
            try { await authenticateGate.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return AccountAuthLifecycleResult.Cancelled(); }
            try
            {
                if (!store.IsAvailable) return AccountAuthLifecycleResult.RecoveryRequired(null);
                byte[]? raw;
                try { raw = await store.ReadAsync(configuration.SessionStorageKey, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return AccountAuthLifecycleResult.Cancelled(); }
                catch { return AccountAuthLifecycleResult.RecoveryRequired(null); }

                if (raw == null)
                {
                    try { await store.WriteAsync(configuration.SessionStorageKey, StoredSessionRecord.Pending().Encode(), cancellationToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return AccountAuthLifecycleResult.Cancelled(); }
                    catch { return AccountAuthLifecycleResult.RecoveryRequired(null); }
                    return await SignUpAsync(cancellationToken).ConfigureAwait(false);
                }

                if (!StoredSessionRecord.TryDecode(raw, out var record) || record == null) return AccountAuthLifecycleResult.RecoveryRequired(null);
                if (record.IsPending) return AccountAuthLifecycleResult.RecoveryRequired(record.Principal(configuration));
                return await RefreshAuthenticatedAsync(record, cancellationToken).ConfigureAwait(false);
            }
            finally { authenticateGate.Release(); }
        }

        private async Task<AccountAuthLifecycleResult> SignUpAsync(CancellationToken cancellationToken)
        {
            var result = await SendAsync("signup", SignupBody, cancellationToken).ConfigureAwait(false);
            if (result.Cancelled) return AccountAuthLifecycleResult.Cancelled();
            if (result.Certainty == HttpDeliveryCertainty.NotSent)
            {
                await ClearPendingAfterNotSentAsync(cancellationToken).ConfigureAwait(false);
                return AccountAuthLifecycleResult.UnavailableOffline(null);
            }
            // A sent anonymous signup can have created a principal even if the response was lost.
            // Retain the pending marker and force explicit recovery rather than creating another guest.
            if (result.Certainty == HttpDeliveryCertainty.Uncertain || result.Response == null) return AccountAuthLifecycleResult.RecoveryRequired(null);
            if (!IsSuccess(result.Response) || !TryDecode(result.Response, out var payload)) return AccountAuthLifecycleResult.RecoveryRequired(null);
            var record = StoredSessionRecord.Active(payload.Subject, payload.RefreshToken);
            try { await store.WriteAsync(configuration.SessionStorageKey, record.Encode(), cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return AccountAuthLifecycleResult.Cancelled(); }
            catch { return AccountAuthLifecycleResult.RecoveryRequired(record.Principal(configuration)); }
            return Authenticated(record, payload);
        }

        private async Task<AccountAuthLifecycleResult> RefreshAuthenticatedAsync(StoredSessionRecord record, CancellationToken cancellationToken)
        {
            var refreshed = await RefreshRecordAsync(record, cancellationToken).ConfigureAwait(false);
            if (refreshed.Cancelled) return AccountAuthLifecycleResult.Cancelled();
            if (refreshed.Unavailable) return AccountAuthLifecycleResult.UnavailableOffline(record.Principal(configuration));
            if (!refreshed.Success || refreshed.Record == null || refreshed.Payload == null) return AccountAuthLifecycleResult.RecoveryRequired(record.Principal(configuration));
            return Authenticated(refreshed.Record, refreshed.Payload);
        }

        private AccountAuthLifecycleResult Authenticated(StoredSessionRecord record, AuthPayload payload)
        {
            var principal = record.Principal(configuration);
            if (principal == null) throw new InvalidOperationException("An active Supabase Auth session requires a principal.");
            return AccountAuthLifecycleResult.Authenticated(principal, new SupabaseAuthSession(this, record, payload));
        }

        private async Task<RefreshResult> RefreshRecordAsync(StoredSessionRecord oldRecord, CancellationToken cancellationToken)
        {
            var result = await SendAsync("token?grant_type=refresh_token", RefreshBody(oldRecord.RefreshToken!), cancellationToken).ConfigureAwait(false);
            if (result.Cancelled) return RefreshResult.CancelledResult();
            if (result.Certainty == HttpDeliveryCertainty.NotSent || result.Certainty == HttpDeliveryCertainty.Uncertain || result.Response == null)
                return RefreshResult.UnavailableResult();
            if (IsTransient(result.Response.StatusCode)) return RefreshResult.UnavailableResult();
            if (!IsSuccess(result.Response) || !TryDecode(result.Response, out var payload) || !string.Equals(payload.Subject, oldRecord.Subject, StringComparison.Ordinal))
                return RefreshResult.RecoveryResult();
            var updated = StoredSessionRecord.Active(payload.Subject, payload.RefreshToken);
            try { await store.WriteAsync(configuration.SessionStorageKey, updated.Encode(), cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return RefreshResult.CancelledResult(); }
            catch { return RefreshResult.RecoveryResult(); }
            return RefreshResult.SuccessResult(updated, payload);
        }

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

        private async Task ClearPendingAfterNotSentAsync(CancellationToken cancellationToken)
        {
            try { await store.DeleteAsync(configuration.SessionStorageKey, cancellationToken).ConfigureAwait(false); }
            catch { /* The next launch must fail closed if a stale marker remains. */ }
        }

        private static bool IsSuccess(HttpResponseData response) => response.StatusCode == 200 || response.StatusCode == 201;
        private static bool IsTransient(int statusCode) => statusCode == 408 || statusCode == 429 || statusCode == 502 || statusCode == 503 || statusCode == 504 || statusCode >= 500;
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

        private sealed class SupabaseAuthSession : IAuthSession
        {
            private readonly SupabaseAnonymousAuthLifecycle owner;
            private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
            private StoredSessionRecord record;
            private AuthPayload payload;
            private long generation;

            internal SupabaseAuthSession(SupabaseAnonymousAuthLifecycle owner, StoredSessionRecord record, AuthPayload payload)
            {
                this.owner = owner; this.record = record; this.payload = payload;
            }

            public string SessionKey => owner.configuration.BackendNamespace.Value + ":" + record.Subject;

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
                var refreshed = await owner.RefreshRecordAsync(record, cancellationToken).ConfigureAwait(false);
                if (refreshed.Cancelled) throw new OperationCanceledException(cancellationToken);
                if (!refreshed.Success || refreshed.Record == null || refreshed.Payload == null)
                    throw new InvalidOperationException("The Supabase Auth session requires recovery or is temporarily unavailable.");
                record = refreshed.Record; payload = refreshed.Payload; generation = checked(generation + 1); return Snapshot();
            }

            private AccessTokenSnapshot Snapshot() => new AccessTokenSnapshot(payload.AccessToken, generation);
        }

        private sealed class SendResult
        {
            private SendResult(HttpResponseData? response, HttpDeliveryCertainty? certainty, bool cancelled) { Response = response; Certainty = certainty; Cancelled = cancelled; }
            public HttpResponseData? Response { get; } public HttpDeliveryCertainty? Certainty { get; } public bool Cancelled { get; }
            public static SendResult ResponseResult(HttpResponseData response) => new SendResult(response, null, false);
            public static SendResult Failed(HttpDeliveryCertainty certainty) => new SendResult(null, certainty, false);
            public static SendResult CancelledResult() => new SendResult(null, null, true);
        }

        private sealed class RefreshResult
        {
            private RefreshResult(bool success, bool unavailable, bool cancelled, StoredSessionRecord? record, AuthPayload? payload) { Success = success; Unavailable = unavailable; Cancelled = cancelled; Record = record; Payload = payload; }
            public bool Success { get; } public bool Unavailable { get; } public bool Cancelled { get; } public StoredSessionRecord? Record { get; } public AuthPayload? Payload { get; }
            public static RefreshResult SuccessResult(StoredSessionRecord record, AuthPayload payload) => new RefreshResult(true, false, false, record, payload);
            public static RefreshResult UnavailableResult() => new RefreshResult(false, true, false, null, null);
            public static RefreshResult RecoveryResult() => new RefreshResult(false, false, false, null, null);
            public static RefreshResult CancelledResult() => new RefreshResult(false, false, true, null, null);
        }

        private sealed class StoredSessionRecord
        {
            private static readonly byte[] Prefix = { (byte)'G', (byte)'P', (byte)'S', (byte)'1', 1 };
            private StoredSessionRecord(bool pending, string? subject, string? refreshToken) { IsPending = pending; Subject = subject; RefreshToken = refreshToken; }
            public bool IsPending { get; } public string? Subject { get; } public string? RefreshToken { get; }
            public static StoredSessionRecord Pending() => new StoredSessionRecord(true, null, null);
            public static StoredSessionRecord Active(string subject, string refreshToken) => new StoredSessionRecord(false, subject, refreshToken);
            public AccountPrincipalDescriptor? Principal(SupabaseAuthConfiguration configuration) => Subject == null ? null : new AccountPrincipalDescriptor(configuration.BackendNamespace, configuration.PrincipalIssuer, Subject);
            public byte[] Encode()
            {
                if (IsPending) return new[] { Prefix[0], Prefix[1], Prefix[2], Prefix[3], Prefix[4], (byte)0 };
                var subject = Encoding.UTF8.GetBytes(Subject!); var token = Encoding.UTF8.GetBytes(RefreshToken!);
                if (subject.Length == 0 || subject.Length > 64 || token.Length == 0 || token.Length > 16_384) throw new InvalidOperationException("The Supabase Auth session record is invalid.");
                var result = new byte[10 + subject.Length + token.Length];
                Buffer.BlockCopy(Prefix, 0, result, 0, Prefix.Length); result[5] = 1;
                result[6] = (byte)(subject.Length >> 8); result[7] = (byte)subject.Length; result[8] = (byte)(token.Length >> 8); result[9] = (byte)token.Length;
                Buffer.BlockCopy(subject, 0, result, 10, subject.Length); Buffer.BlockCopy(token, 0, result, 10 + subject.Length, token.Length); return result;
            }
            public static bool TryDecode(byte[] bytes, out StoredSessionRecord? value)
            {
                value = null;
                if (bytes == null || bytes.Length < 6 || bytes[0] != Prefix[0] || bytes[1] != Prefix[1] || bytes[2] != Prefix[2] || bytes[3] != Prefix[3] || bytes[4] != Prefix[4]) return false;
                if (bytes[5] == 0) { if (bytes.Length != 6) return false; value = Pending(); return true; }
                if (bytes[5] != 1 || bytes.Length < 10) return false;
                var subjectLength = (bytes[6] << 8) | bytes[7]; var tokenLength = (bytes[8] << 8) | bytes[9];
                if (subjectLength == 0 || subjectLength > 64 || tokenLength == 0 || tokenLength > 16_384 || bytes.Length != 10 + subjectLength + tokenLength) return false;
                try
                {
                    var subject = new UTF8Encoding(false, true).GetString(bytes, 10, subjectLength);
                    var token = new UTF8Encoding(false, true).GetString(bytes, 10 + subjectLength, tokenLength);
                    if (!Guid.TryParseExact(subject, "D", out var id) || id == Guid.Empty || !ValidToken(token)) return false;
                    value = Active(id.ToString("D"), token); return true;
                }
                catch { return false; }
            }
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
