using System;

namespace GamePlatform.Core
{

    public readonly struct ScopedOwnerContext : IEquatable<ScopedOwnerContext>
    {
        public ScopedOwnerContext(OwnerScope owner, SemanticId viewKey, long generation)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A valid owner is required.", nameof(owner));
            if (!viewKey.IsValid)
                throw new ArgumentException("A view key is required.", nameof(viewKey));
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            Owner = owner;
            ViewKey = viewKey;
            Generation = generation;
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
