using System;
using GamePlatform.Core;

namespace GamePlatform.Transport.Http
{
    public sealed class BackendHttpConfiguration
    {
        public BackendHttpConfiguration(Uri baseUri, BackendNamespace backendNamespace, int maximumResponseBytes = 262_144)
        {
            if (baseUri == null) throw new ArgumentNullException(nameof(baseUri));
            if (!baseUri.IsAbsoluteUri || !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("The backend URI must be absolute HTTPS.", nameof(baseUri));
            if (!string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment) || !string.IsNullOrEmpty(baseUri.UserInfo)) throw new ArgumentException("The backend URI cannot contain credentials, query or fragment components.", nameof(baseUri));
            if (!backendNamespace.IsValid) throw new ArgumentException("A backend namespace is required.", nameof(backendNamespace));
            if (maximumResponseBytes <= 0 || maximumResponseBytes > 262_144) throw new ArgumentOutOfRangeException(nameof(maximumResponseBytes));
            BaseUri = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
            BackendNamespace = backendNamespace;
            MaximumResponseBytes = maximumResponseBytes;
        }
        public Uri BaseUri { get; }
        public BackendNamespace BackendNamespace { get; }
        public int MaximumResponseBytes { get; }
        public Uri Resolve(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || relativePath[0] == '/' || relativePath.Contains("..")) throw new ArgumentException("A bounded relative operation path is required.", nameof(relativePath));
            var resolved = new Uri(BaseUri, relativePath);
            if (!string.Equals(resolved.Scheme, BaseUri.Scheme, StringComparison.OrdinalIgnoreCase) || !string.Equals(resolved.Host, BaseUri.Host, StringComparison.OrdinalIgnoreCase) || resolved.Port != BaseUri.Port) throw new InvalidOperationException("The operation path escaped the configured backend authority.");
            return resolved;
        }
    }
}
