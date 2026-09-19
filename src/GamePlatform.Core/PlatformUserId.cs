using System;

namespace GamePlatform.Core
{
    /// <summary>Immutable UUID identity. Issued RFC UUID versions 1-8 are preserved; generate new IDs with UuidV7Generator.</summary>
    public readonly struct PlatformUserId : IEquatable<PlatformUserId>, IComparable<PlatformUserId>
    {
        public PlatformUserId(Guid value)
        {
            if (!UuidIdentity.IsValid(value)) throw new ArgumentException("An RFC UUID is required.", nameof(value));
            Value = value;
        }
        public Guid Value { get; }
        public bool IsValid => UuidIdentity.IsValid(Value);
        public static PlatformUserId FromRfcBytes(ReadOnlySpan<byte> bytes) => new PlatformUserId(UuidIdentity.FromRfcBytes(bytes));
        public void WriteRfcBytes(Span<byte> destination) => UuidIdentity.WriteRfcBytes(Value, destination);
        public bool Equals(PlatformUserId other) => Value.Equals(other.Value);
        public override bool Equals(object? obj) => obj is PlatformUserId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(PlatformUserId other) => UuidIdentity.Compare(Value, other.Value);
        public override string ToString() => Value.ToString("D");
        public static bool operator ==(PlatformUserId left, PlatformUserId right) => left.Equals(right);
        public static bool operator !=(PlatformUserId left, PlatformUserId right) => !left.Equals(right);
    }
}
