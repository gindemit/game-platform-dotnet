using System;
namespace GamePlatform.Navigation.Kernel
{
    public readonly struct FlowId : IEquatable<FlowId>
    {
        internal FlowId(long value) { Value = value; }
        public long Value { get; }
        public bool Equals(FlowId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is FlowId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(FlowId left, FlowId right) => left.Equals(right);
        public static bool operator !=(FlowId left, FlowId right) => !left.Equals(right);
    }
}
