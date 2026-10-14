using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Accounts
{

    /// <summary>Consumer-owned account directory. It records no credential and never asserts sync readiness.</summary>
    public interface IAccountDirectoryStore
    {
        Task<bool> HasAnyEntryAsync(CancellationToken cancellationToken);
        Task<AccountDirectoryEntry?> FindAsync(AccountPrincipalDescriptor principal, AppId appId, CancellationToken cancellationToken);
        Task<AccountDirectoryEntry> ReserveAsync(AccountPrincipalDescriptor principal, AppId appId, Guid installationId, ClientStreamId streamId, CancellationToken cancellationToken);
        Task<AccountDirectoryEntry> BindIssuedAccountAsync(AccountDirectoryEntry reservation, PlatformUserId accountId, string membershipStatus, CancellationToken cancellationToken);
    }
}
