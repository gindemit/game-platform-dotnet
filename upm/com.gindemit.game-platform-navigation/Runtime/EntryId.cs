using System;
namespace GamePlatform.Navigation.Kernel
{
    public readonly struct EntryId : IEquatable<EntryId>
    {
        internal EntryId(long value) { Value = value; }
        public long Value { get; }
        public bool Equals(EntryId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is EntryId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(EntryId left, EntryId right) => left.Equals(right);
        public static bool operator !=(EntryId left, EntryId right) => !left.Equals(right);
    }
}
