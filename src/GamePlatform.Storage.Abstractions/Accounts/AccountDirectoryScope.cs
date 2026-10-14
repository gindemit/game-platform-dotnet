using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Accounts
{
    /// <summary>Account-neutral directory ownership, available before a platform user is issued.</summary>
    public readonly struct AccountDirectoryScope : IEquatable<AccountDirectoryScope>
    {
        public AccountDirectoryScope(BackendNamespace backendNamespace, AppId appId)
        {
            if (!backendNamespace.IsValid)
                throw new ArgumentException("A valid backend namespace is required.", nameof(backendNamespace));
            if (!appId.IsValid)
                throw new ArgumentException("A valid app ID is required.", nameof(appId));
            BackendNamespace = backendNamespace;
            AppId = appId;
        }

        public BackendNamespace BackendNamespace { get; }
        public AppId AppId { get; }
        public bool IsValid => BackendNamespace.IsValid && AppId.IsValid;
        public bool Equals(AccountDirectoryScope other) => BackendNamespace == other.BackendNamespace && AppId == other.AppId;
        public override bool Equals(object? obj) => obj is AccountDirectoryScope other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(BackendNamespace, AppId);
    }
}
