using System;

namespace GamePlatform.Diagnostics.Abstractions
{

    /// <summary>Process-local correlation, captured when a scope is constructed. Never supply a provider/user ID.</summary>
    public sealed class AppLogContext
    {
        public AppLogContext(Guid correlationId, long generation)
        {
            if (correlationId == Guid.Empty)
                throw new ArgumentException("Correlation must be nonempty.", nameof(correlationId));
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            CorrelationId = correlationId;
            Generation = generation;
        }

        public Guid CorrelationId { get; }
        public long Generation { get; }
    }
}
