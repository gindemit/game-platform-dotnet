using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;
using GamePlatform.Features.Contracts;

namespace GamePlatform.Features.Progression
{

    /// <summary>A complete authoritative projection boundary supplied by bootstrap/pull. Its revision is owned by the synchronization adapter.</summary>
    public sealed class ProgressionConfirmedProjection
    {
        private readonly ReadOnlyCollection<ProgressionConfirmedState> states;
        public ProgressionConfirmedProjection(long revision, long confirmedAtMilliseconds, IReadOnlyList<ProgressionConfirmedState> states)
        {
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            ProgressionValidation.Timestamp(confirmedAtMilliseconds, nameof(confirmedAtMilliseconds));
            if (states == null || states.Count > 1024 || states.Any(value => value == null))
                throw new ArgumentOutOfRangeException(nameof(states));
            if (states.GroupBy(value => value.StateKey).Any(group => group.Count() != 1))
                throw new ArgumentException("Confirmed progression state keys must be unique.", nameof(states));
            Revision = revision;
            ConfirmedAtMilliseconds = confirmedAtMilliseconds;
            this.states = new ReadOnlyCollection<ProgressionConfirmedState>(states.ToArray());
        }
        public long Revision { get; }
        public long ConfirmedAtMilliseconds { get; }
        public IReadOnlyList<ProgressionConfirmedState> States => states;
    }
}
