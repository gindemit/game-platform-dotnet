using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions
{
    public readonly struct StorageScope : IEquatable<StorageScope>
    {
        public StorageScope(string backendNamespace, PlatformId appId, PlatformId accountId)
        {
            if (string.IsNullOrWhiteSpace(backendNamespace) || backendNamespace.Length > 128)
                throw new ArgumentOutOfRangeException(nameof(backendNamespace));
            if (backendNamespace.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("Backend namespace contains a forbidden character.", nameof(backendNamespace));
            if (!appId.IsValid) throw new ArgumentException("A valid app ID is required.", nameof(appId));
            if (!accountId.IsValid) throw new ArgumentException("A valid account ID is required.", nameof(accountId));
            BackendNamespace = backendNamespace;
            AppId = appId;
            AccountId = accountId;
        }

        public string BackendNamespace { get; }
        public PlatformId AppId { get; }
        public PlatformId AccountId { get; }

        public bool Equals(StorageScope other) =>
            string.Equals(BackendNamespace, other.BackendNamespace, StringComparison.Ordinal) &&
            AppId.Equals(other.AppId) && AccountId.Equals(other.AccountId);

        public override bool Equals(object? obj) => obj is StorageScope other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(BackendNamespace ?? string.Empty);
                hash = (hash * 397) ^ AppId.GetHashCode();
                return (hash * 397) ^ AccountId.GetHashCode();
            }
        }
    }

    public interface ILocalStorageTransaction
    {
        StorageScope Scope { get; }
    }

    public interface ISerializedStorageExecutor
    {
        Task<T> ExecuteAsync<T>(
            StorageScope scope,
            Func<ILocalStorageTransaction, T> operation,
            CancellationToken cancellationToken);
    }

    public enum StorageFailure
    {
        Busy,
        Capacity,
        Io,
        Corrupt,
        Constraint,
        Migration,
        InvalidOwner,
        Closing,
        NotReady,
        IdentityConflict,
        SequenceExhausted,
        StreamRecoveryRequired,
        Unavailable
    }

    public sealed class StorageException : Exception
    {
        public StorageException(StorageFailure failure, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Failure = failure;
        }

        public StorageFailure Failure { get; }
    }
}
