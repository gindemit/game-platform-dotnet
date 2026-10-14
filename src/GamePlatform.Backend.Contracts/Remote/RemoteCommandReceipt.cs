using System;

namespace GamePlatform.Backend.Contracts.Remote
{

    public sealed class RemoteCommandReceipt
    {
        public RemoteCommandReceipt(bool found, RemoteCommandOutcome? outcome, long observedFinalizedThrough)
        {
            if (found != (outcome != null))
                throw new ArgumentException("A found receipt requires exactly one terminal outcome.", nameof(outcome));
            if (observedFinalizedThrough < 0)
                throw new ArgumentOutOfRangeException(nameof(observedFinalizedThrough));
            Found = found;
            Outcome = outcome;
            ObservedFinalizedThrough = observedFinalizedThrough;
        }

        public bool Found { get; }
        public RemoteCommandOutcome? Outcome { get; }
        public long ObservedFinalizedThrough { get; }
    }
}
