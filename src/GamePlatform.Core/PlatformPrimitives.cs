using System;

namespace GamePlatform.Core
{
    public enum PlatformFailure
    {
        None,
        Offline,
        Unauthorized,
        Conflict,
        NotFound,
        RateLimited,
        Invalid,
        Unavailable,
        NotImplemented
    }

    public readonly struct PlatformId : IEquatable<PlatformId>
    {
        public PlatformId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("An ID is required.", nameof(value));
            if (value.Length > 128) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value) && Value.Length <= 128;
        public bool Equals(PlatformId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is PlatformId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
    }

    public sealed class PlatformResult<T>
    {
        private PlatformResult(T? value, PlatformFailure failure, string diagnostic)
        {
            Value = value;
            Failure = failure;
            Diagnostic = diagnostic;
        }

        public T? Value { get; }
        public PlatformFailure Failure { get; }
        public string Diagnostic { get; }
        public bool Succeeded => Failure == PlatformFailure.None;

        public static PlatformResult<T> Success(T value) => new PlatformResult<T>(value, PlatformFailure.None, string.Empty);
        public static PlatformResult<T> Error(PlatformFailure failure, string diagnostic = "")
        {
            if (failure == PlatformFailure.None) throw new ArgumentException("An error requires a failure code.", nameof(failure));
            return new PlatformResult<T>(default, failure, diagnostic ?? string.Empty);
        }
    }

    public sealed class PlatformCapabilityUnavailableException : NotSupportedException
    {
        public PlatformCapabilityUnavailableException(string capability, string reason)
            : base($"Capability '{capability}' is unavailable: {reason}")
        {
            Capability = capability;
            Reason = reason;
        }

        public string Capability { get; }
        public string Reason { get; }
    }
}
