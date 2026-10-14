using System;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Accounts
{

    /// <summary>Durable, non-secret reservation. Issued account data is absent until provision succeeds.</summary>
    public sealed class AccountDirectoryEntry
    {
        public AccountDirectoryEntry(AccountPrincipalDescriptor principal, AppId appId, Guid installationId, ClientStreamId streamId, PlatformUserId? accountId, string? membershipStatus)
        {
            Principal = principal ?? throw new ArgumentNullException(nameof(principal));
            if (!appId.IsValid || installationId == Guid.Empty || !streamId.IsValid)
                throw new ArgumentException("A valid app, installation and stream are required.");
            if ((accountId.HasValue) != !string.IsNullOrWhiteSpace(membershipStatus))
                throw new ArgumentException("Issued account and membership must be present together.");
            if (membershipStatus != null && (membershipStatus.Length > 64 || membershipStatus.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0))
                throw new ArgumentOutOfRangeException(nameof(membershipStatus));
            AppId = appId;
            InstallationId = installationId;
            StreamId = streamId;
            AccountId = accountId;
            MembershipStatus = membershipStatus;
        }

        public AccountPrincipalDescriptor Principal { get; }
        public AppId AppId { get; }
        public Guid InstallationId { get; }
        public ClientStreamId StreamId { get; }
        public PlatformUserId? AccountId { get; }
        public string? MembershipStatus { get; }
        public bool HasIssuedAccount => AccountId.HasValue;
    }
}
