using System;

namespace GamePlatform.Core
{

    public readonly struct PlatformId : IEquatable<PlatformId>
    {
        public PlatformId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("An ID is required.", nameof(value));
            if (value.Length > 128)
                throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value) && Value.Length <= 128;
        public bool Equals(PlatformId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is PlatformId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
    }
}
