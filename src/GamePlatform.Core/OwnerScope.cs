using System;

namespace GamePlatform.Core
{
    public readonly struct OwnerScope : IEquatable<OwnerScope>
    {
        public OwnerScope(BackendNamespace backend, AppId appId, PlatformUserId userId)
        {
            if (!backend.IsValid)
                throw new ArgumentException("A backend is required.", nameof(backend));
            if (!appId.IsValid)
                throw new ArgumentException("An app is required.", nameof(appId));
            if (!userId.IsValid)
                throw new ArgumentException("A user is required.", nameof(userId));
            Backend = backend;
            AppId = appId;
            UserId = userId;
        }
        public BackendNamespace Backend { get; }
        public AppId AppId { get; }
        public PlatformUserId UserId { get; }
        public bool IsValid => Backend.IsValid && AppId.IsValid && UserId.IsValid;
        public bool Equals(OwnerScope other) => Backend == other.Backend && AppId == other.AppId && UserId == other.UserId;
        public override bool Equals(object? obj) => obj is OwnerScope other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Backend, AppId, UserId);
        public static bool operator ==(OwnerScope left, OwnerScope right) => left.Equals(right);
        public static bool operator !=(OwnerScope left, OwnerScope right) => !left.Equals(right);
    }
}
