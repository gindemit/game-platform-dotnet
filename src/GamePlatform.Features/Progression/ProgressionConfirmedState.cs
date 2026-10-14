using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Progression
{

    /// <summary>One server-owned semantic progression key/value. It is not a game checkpoint and may not be inferred from completion.</summary>
    public sealed class ProgressionConfirmedState
    {
        public ProgressionConfirmedState(SemanticId stateKey, long value)
        {
            if (!stateKey.IsValid)
                throw new ArgumentException("A state key is required.", nameof(stateKey));
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            StateKey = stateKey;
            Value = value;
        }
        public SemanticId StateKey { get; }
        public long Value { get; }
    }
}
