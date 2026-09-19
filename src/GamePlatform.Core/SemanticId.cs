using System;

namespace GamePlatform.Core
{
    /// <summary>Ordinal semantic identity; does not reinterpret provider/content IDs as UUIDs.</summary>
    public readonly struct SemanticId : IEquatable<SemanticId>
    {
        public SemanticId(string value) { Value = new PlatformId(value).Value; }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value) && Value.Length <= 128;
        public bool Equals(SemanticId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is SemanticId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(SemanticId left, SemanticId right) => left.Equals(right);
        public static bool operator !=(SemanticId left, SemanticId right) => !left.Equals(right);
    }
}
