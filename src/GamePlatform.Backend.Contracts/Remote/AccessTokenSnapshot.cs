using System;

namespace GamePlatform.Backend.Contracts.Remote
{
    public readonly struct AccessTokenSnapshot
    {
        public AccessTokenSnapshot(string value, long generation)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("A bounded opaque access token is required.", nameof(value));
            if (value.Length > 16_384)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            Value = value;
            Generation = generation;
        }
        public string Value { get; }
        public long Generation { get; }
    }
}
