using System;

namespace GamePlatform.Core
{

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
            if (failure == PlatformFailure.None)
                throw new ArgumentException("An error requires a failure code.", nameof(failure));
            if (!Enum.IsDefined(typeof(PlatformFailure), failure))
                throw new ArgumentOutOfRangeException(nameof(failure));
            if (diagnostic != null && diagnostic.Length > 512)
                throw new ArgumentOutOfRangeException(nameof(diagnostic));
            return new PlatformResult<T>(default, failure, diagnostic ?? string.Empty);
        }
    }
}
