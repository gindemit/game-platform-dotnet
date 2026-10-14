using System;

namespace GamePlatform.Transport.Abstractions
{

    public sealed class HttpExecutionException : Exception
    {
        public HttpExecutionException(HttpDeliveryCertainty certainty, string message, Exception? innerException = null) : base(message, innerException) { Certainty = certainty; }
        public HttpDeliveryCertainty Certainty { get; }
    }
}
