#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Accounts
{
    /// <summary>Stable, non-secret provider identity. It is never derived from an access token or session key.</summary>
    public sealed class AccountPrincipalDescriptor : IEquatable<AccountPrincipalDescriptor>
    {
        public AccountPrincipalDescriptor(BackendNamespace backendNamespace, string issuer, string subject)
        {
            if (!backendNamespace.IsValid) throw new ArgumentException("A backend namespace is required.", nameof(backendNamespace));
            Validate(issuer, 256, nameof(issuer)); Validate(subject, 1024, nameof(subject));
            BackendNamespace = backendNamespace; Issuer = issuer; Subject = subject;
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

    /// <summary>Durable, non-secret reservation. Issued account data is absent until provision succeeds.</summary>
    public sealed class AccountDirectoryEntry
    {
        public AccountDirectoryEntry(AccountPrincipalDescriptor principal, AppId appId, Guid installationId, ClientStreamId streamId, PlatformUserId? accountId, string? membershipStatus)
        {
            Principal = principal ?? throw new ArgumentNullException(nameof(principal));
            if (!appId.IsValid || installationId == Guid.Empty || !streamId.IsValid) throw new ArgumentException("A valid app, installation and stream are required.");
            if ((accountId.HasValue) != !string.IsNullOrWhiteSpace(membershipStatus)) throw new ArgumentException("Issued account and membership must be present together.");
            if (membershipStatus != null && (membershipStatus.Length > 64 || membershipStatus.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)) throw new ArgumentOutOfRangeException(nameof(membershipStatus));
            AppId = appId; InstallationId = installationId; StreamId = streamId; AccountId = accountId; MembershipStatus = membershipStatus;
        }

        public AccountPrincipalDescriptor Principal { get; }
        public AppId AppId { get; }
        public Guid InstallationId { get; }
        public ClientStreamId StreamId { get; }
        public PlatformUserId? AccountId { get; }
        public string? MembershipStatus { get; }
        public bool HasIssuedAccount => AccountId.HasValue;
    }

    /// <summary>Consumer-owned account directory. It records no credential and never asserts sync readiness.</summary>
    public interface IAccountDirectoryStore
    {
        Task<AccountDirectoryEntry?> FindAsync(AccountPrincipalDescriptor principal, AppId appId, CancellationToken cancellationToken);
        Task<AccountDirectoryEntry> ReserveAsync(AccountPrincipalDescriptor principal, AppId appId, Guid installationId, ClientStreamId streamId, CancellationToken cancellationToken);
        Task<AccountDirectoryEntry> BindIssuedAccountAsync(AccountDirectoryEntry reservation, PlatformUserId accountId, string membershipStatus, CancellationToken cancellationToken);
    }
}
