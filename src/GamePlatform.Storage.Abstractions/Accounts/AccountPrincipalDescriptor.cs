using System;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Accounts
{

    /// <summary>Stable, non-secret provider identity. It is never derived from an access token or session key.</summary>
    public sealed class AccountPrincipalDescriptor : IEquatable<AccountPrincipalDescriptor>
    {
        public AccountPrincipalDescriptor(BackendNamespace backendNamespace, string issuer, string subject)
        {
            if (!backendNamespace.IsValid)
                throw new ArgumentException("A backend namespace is required.", nameof(backendNamespace));
            Validate(issuer, 256, nameof(issuer));
            Validate(subject, 1024, nameof(subject));
            BackendNamespace = backendNamespace;
            Issuer = issuer;
            Subject = subject;
        }

        public BackendNamespace BackendNamespace { get; }
        public string Issuer { get; }
        public string Subject { get; }
        public bool Equals(AccountPrincipalDescriptor? other) => other != null && BackendNamespace == other.BackendNamespace && string.Equals(Issuer, other.Issuer, StringComparison.Ordinal) && string.Equals(Subject, other.Subject, StringComparison.Ordinal);
        public override bool Equals(object? obj) => Equals(obj as AccountPrincipalDescriptor);
        public override int GetHashCode() => HashCode.Combine(BackendNamespace, Issuer, Subject);

        private static void Validate(string value, int maximum, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
