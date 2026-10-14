using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts
{

    public sealed class ProgressEvent
    {
        public ProgressEvent(long sequence, string counter, long amount)
        {
            if (sequence <= 0 || amount < 0)
                throw new ArgumentOutOfRangeException(nameof(sequence));
            Sequence = sequence;
            Counter = CompatibilityGuard.Required(counter, nameof(counter), 128);
            Amount = amount;
        }
        public long Sequence { get; }
        public string Counter { get; }
        public long Amount { get; }
    }
}
