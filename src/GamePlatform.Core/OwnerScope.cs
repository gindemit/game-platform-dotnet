using System;

namespace GamePlatform.Core
{
    public readonly struct OwnerScope : IEquatable<OwnerScope>
    {
        public OwnerScope(BackendNamespace backend, AppId appId, PlatformUserId userId)
        {
            if (!backend.IsValid) throw new ArgumentException("A backend is required.", nameof(backend));
            if (!appId.IsValid) throw new ArgumentException("An app is required.", nameof(appId));
            if (!userId.IsValid) throw new ArgumentException("A user is required.", nameof(userId));
            Backend = backend; AppId = appId; UserId = userId;
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

    public readonly struct ScopedOwnerContext : IEquatable<ScopedOwnerContext>
    {
        public ScopedOwnerContext(OwnerScope owner, SemanticId viewKey, long generation)
        {
            if (!owner.IsValid) throw new ArgumentException("A valid owner is required.", nameof(owner));
            if (!viewKey.IsValid) throw new ArgumentException("A view key is required.", nameof(viewKey));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            Owner = owner; ViewKey = viewKey; Generation = generation;
        }
        public OwnerScope Owner { get; }
        public SemanticId ViewKey { get; }
        public long Generation { get; }
        public bool IsValid => Owner.IsValid && ViewKey.IsValid && Generation >= 0;
        public bool Equals(ScopedOwnerContext other) => Owner == other.Owner && ViewKey == other.ViewKey && Generation == other.Generation;
        public override bool Equals(object? obj) => obj is ScopedOwnerContext other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Owner, ViewKey, Generation);
        public static bool operator ==(ScopedOwnerContext left, ScopedOwnerContext right) => left.Equals(right);
        public static bool operator !=(ScopedOwnerContext left, ScopedOwnerContext right) => !left.Equals(right);
    }
}
