using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Snapshots
{
    /// <summary>A feature-owned canonical query identity inside one captured private view.</summary>
    public readonly struct ScopedQueryKey : IEquatable<ScopedQueryKey>
    {
        public ScopedQueryKey(ScopedOwnerContext owner, string featureNamespace, string canonicalQuery)
        {
            if (!owner.IsValid) throw new ArgumentException("A valid owner is required.", nameof(owner));
            Validate(featureNamespace, 128, nameof(featureNamespace));
            Validate(canonicalQuery, 1024, nameof(canonicalQuery));
            Owner = owner;
            FeatureNamespace = featureNamespace;
            CanonicalQuery = canonicalQuery;
        }

        public ScopedOwnerContext Owner { get; }
        public string FeatureNamespace { get; }
        public string CanonicalQuery { get; }

        public bool Equals(ScopedQueryKey other) => Owner == other.Owner &&
            string.Equals(FeatureNamespace, other.FeatureNamespace, StringComparison.Ordinal) &&
            string.Equals(CanonicalQuery, other.CanonicalQuery, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ScopedQueryKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Owner,
            StringComparer.Ordinal.GetHashCode(FeatureNamespace ?? string.Empty),
            StringComparer.Ordinal.GetHashCode(CanonicalQuery ?? string.Empty));
        public static bool operator ==(ScopedQueryKey left, ScopedQueryKey right) => left.Equals(right);
        public static bool operator !=(ScopedQueryKey left, ScopedQueryKey right) => !left.Equals(right);

        private static void Validate(string value, int maximum, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
