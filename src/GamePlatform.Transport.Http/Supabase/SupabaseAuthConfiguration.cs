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

    /// <summary>
    /// Host-owned secure storage for the small opaque refresh-session record.
    /// The Unity integration adapts its existing Android-keystore secret port;
    /// this SDK assembly deliberately has no Unity reference or fallback store.
    /// </summary>
    public interface ISupabaseAnonymousSessionStore
    {
        bool IsAvailable { get; }
        System.Threading.Tasks.Task<byte[]?> ReadAsync(string key, System.Threading.CancellationToken cancellationToken);
        System.Threading.Tasks.Task WriteAsync(string key, byte[] secret, System.Threading.CancellationToken cancellationToken);
        System.Threading.Tasks.Task DeleteAsync(string key, System.Threading.CancellationToken cancellationToken);
    }
}
