using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GamePlatform.Features.Progression
{

    public sealed class ProgressionSnapshot
    {
        private readonly ReadOnlyCollection<PendingProgressionCompletion> pending;
        public ProgressionSnapshot(ProgressionConfirmedProjection? confirmed, IReadOnlyList<PendingProgressionCompletion> pending)
        {
            if (pending == null || pending.Any(value => value == null))
                throw new ArgumentNullException(nameof(pending));
            Confirmed = confirmed;
            this.pending = new ReadOnlyCollection<PendingProgressionCompletion>(pending.ToArray());
        }
        public ProgressionConfirmedProjection? Confirmed { get; }
        public IReadOnlyList<PendingProgressionCompletion> PendingCompletions => pending;
    }
}
