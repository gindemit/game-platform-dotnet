#nullable enable
using System;
using GamePlatform.Core;

namespace GamePlatform.Transport.Http.Supabase
{
    /// <summary>
    /// Nonproduction Supabase Auth boundary configuration. The publishable key is
    /// transport input only; it is never persisted or logged. JWT issuer and
    /// audience validation remain the backend's responsibility.
    /// </summary>
    public sealed class SupabaseAuthConfiguration
    {
        public SupabaseAuthConfiguration(Uri authBaseUri, string principalIssuer,
            BackendNamespace backendNamespace, string publishableApiKey,
            string sessionStorageKey, int maximumResponseBytes = 65_536)
        {
            if (authBaseUri == null) throw new ArgumentNullException(nameof(authBaseUri));
            if (!authBaseUri.IsAbsoluteUri || !string.Equals(authBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(authBaseUri.Query) || !string.IsNullOrEmpty(authBaseUri.Fragment) || !string.IsNullOrEmpty(authBaseUri.UserInfo))
                throw new ArgumentException("The Supabase Auth base URI must be absolute HTTPS without credentials, query or fragment.", nameof(authBaseUri));
            ValidateText(principalIssuer, 256, nameof(principalIssuer));
            if (!backendNamespace.IsValid) throw new ArgumentException("A backend namespace is required.", nameof(backendNamespace));
            ValidateText(publishableApiKey, 4_096, nameof(publishableApiKey));
            ValidateStorageKey(sessionStorageKey);
            if (maximumResponseBytes < 1_024 || maximumResponseBytes > 65_536) throw new ArgumentOutOfRangeException(nameof(maximumResponseBytes));

            AuthBaseUri = new Uri(authBaseUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
            if (!AuthBaseUri.AbsolutePath.EndsWith("/auth/v1/", StringComparison.Ordinal))
                throw new ArgumentException("The Supabase Auth base URI must end with /auth/v1/.", nameof(authBaseUri));
            PrincipalIssuer = principalIssuer;
            BackendNamespace = backendNamespace;
            PublishableApiKey = publishableApiKey;
            SessionStorageKey = sessionStorageKey;
            MaximumResponseBytes = maximumResponseBytes;
        }

        public Uri AuthBaseUri { get; }
        public string PrincipalIssuer { get; }
        public BackendNamespace BackendNamespace { get; }
        public string PublishableApiKey { get; }
        public string SessionStorageKey { get; }
        public int MaximumResponseBytes { get; }

        public Uri Resolve(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || relativePath[0] == '/' || relativePath.Contains(".."))
                throw new ArgumentException("A bounded Supabase Auth operation path is required.", nameof(relativePath));
            var resolved = new Uri(AuthBaseUri, relativePath);
            if (!string.Equals(resolved.Scheme, AuthBaseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(resolved.Host, AuthBaseUri.Host, StringComparison.OrdinalIgnoreCase) || resolved.Port != AuthBaseUri.Port)
                throw new InvalidOperationException("The Supabase Auth operation escaped the configured authority.");
            return resolved;
        }

        private static void ValidateText(string value, int maximum, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentOutOfRangeException(name);
        }

        private static void ValidateStorageKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) throw new ArgumentOutOfRangeException(nameof(value));
            foreach (var character in value)
                if (!(char.IsLetterOrDigit(character) || character == '.' || character == '_' || character == '-'))
                    throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public enum SupabaseAnonymousSessionState { FreshAuthorized, SignupPending, Known, RefreshPending, RecoveryRequired }

    /// <summary>Non-secret public marker. It intentionally contains neither access nor refresh credentials.</summary>
    public sealed class SupabaseAnonymousSessionPublicRecord
    {
        public SupabaseAnonymousSessionPublicRecord(long version, SupabaseAnonymousSessionState state, string? subject)
        {
            if (version < 0 || !Enum.IsDefined(typeof(SupabaseAnonymousSessionState), state)) throw new ArgumentOutOfRangeException(nameof(version));
            bool subjectRequired = state == SupabaseAnonymousSessionState.Known || state == SupabaseAnonymousSessionState.RefreshPending;
            bool subjectAllowed = subjectRequired || state == SupabaseAnonymousSessionState.RecoveryRequired;
            if ((subjectRequired && string.IsNullOrWhiteSpace(subject)) || (!subjectAllowed && !string.IsNullOrWhiteSpace(subject)))
                throw new ArgumentException("Session state and subject do not match.", nameof(subject));
            if (subject != null && (!Guid.TryParseExact(subject, "D", out var id) || id == Guid.Empty)) throw new ArgumentOutOfRangeException(nameof(subject));
            Version = version; State = state; Subject = subject == null ? null : Guid.ParseExact(subject, "D").ToString("D");
        }
        public long Version { get; }
        public SupabaseAnonymousSessionState State { get; }
        public string? Subject { get; }
    }

    /// <summary>Defensively copied logical public/secret record. The public portion has no secret.</summary>
    public sealed class SupabaseAnonymousSessionSnapshot
    {
        public SupabaseAnonymousSessionSnapshot(SupabaseAnonymousSessionPublicRecord? @public, byte[]? secret)
        {
            if (@public == null && secret != null) throw new ArgumentException("An absent marker cannot carry a secret.", nameof(secret));
            if (@public != null && @public.Version <= 0) throw new ArgumentOutOfRangeException(nameof(@public));
            Public = @public; this.secret = secret == null ? null : (byte[])secret.Clone();
        }
        private readonly byte[]? secret;
        public SupabaseAnonymousSessionPublicRecord? Public { get; }
        public bool Exists => Public != null;
        public byte[]? CopySecret() => secret == null ? null : (byte[])secret.Clone();
        public static SupabaseAnonymousSessionSnapshot Absent() => new SupabaseAnonymousSessionSnapshot(null, null);
    }

    public sealed class SupabaseAnonymousSessionTransition
    {
        public SupabaseAnonymousSessionTransition(SupabaseAnonymousSessionState state, string? subject)
        {
            State = state; Subject = subject;
            _ = new SupabaseAnonymousSessionPublicRecord(0, state, subject);
        }
        public SupabaseAnonymousSessionState State { get; }
        public string? Subject { get; }
    }

    public sealed class SupabaseAnonymousSessionCompareExchangeResult
    {
        public SupabaseAnonymousSessionCompareExchangeResult(bool applied, SupabaseAnonymousSessionSnapshot current) { Applied = applied; Current = current ?? throw new ArgumentNullException(nameof(current)); }
        public bool Applied { get; }
        public SupabaseAnonymousSessionSnapshot Current { get; }
    }

    /// <summary>
    /// Host-owned versioned logical public+secret store. A successful compare-exchange
    /// atomically publishes a next public marker and matching secret. Implementations
    /// must defend-copy all secret bytes and never emulate CAS with delete/recreate.
    /// </summary>
    public interface ISupabaseAnonymousSessionStore
    {
        bool IsAvailable { get; }
        System.Threading.Tasks.Task<SupabaseAnonymousSessionSnapshot> ReadAsync(string key, System.Threading.CancellationToken cancellationToken);
        System.Threading.Tasks.Task<SupabaseAnonymousSessionCompareExchangeResult> CompareExchangeAsync(string key,
            SupabaseAnonymousSessionSnapshot expected, SupabaseAnonymousSessionTransition next, byte[]? secret,
            System.Threading.CancellationToken cancellationToken);
    }
}
