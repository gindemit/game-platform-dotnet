using System;

namespace GamePlatform.Backend.Contracts.Remote
{

    public readonly struct RemoteFailure
    {
        public RemoteFailure(RemoteFailureKind kind, int? statusCode = null, TimeSpan? retryAfter = null)
        {
            Kind = kind;
            StatusCode = statusCode;
            RetryAfter = retryAfter;
        }
        public RemoteFailureKind Kind { get; }
        public int? StatusCode { get; }
        public TimeSpan? RetryAfter { get; }
    }
}
