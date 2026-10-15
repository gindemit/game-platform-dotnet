using System;

namespace GamePlatform.Navigation.Kernel.Flows
{
    /// <summary>An immutable, versioned safe-resume point returned by the checkpoint store.</summary>
    public sealed class TutorialCheckpoint : IEquatable<TutorialCheckpoint>
    {
        public TutorialCheckpoint(TutorialCheckpointIdentity identity, string stepKey, TutorialCheckpointState state, long revision)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if (string.IsNullOrWhiteSpace(stepKey))
                throw new ArgumentException("A step key is required.", nameof(stepKey));
            if (!Enum.IsDefined(typeof(TutorialCheckpointState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            if (revision < 1)
                throw new ArgumentOutOfRangeException(nameof(revision));
            StepKey = stepKey;
            State = state;
            Revision = revision;
        }

        public TutorialCheckpointIdentity Identity { get; }
        public string StepKey { get; }
        public TutorialCheckpointState State { get; }
        public long Revision { get; }
        public bool IsCompleted => State == TutorialCheckpointState.Completed;

        public bool Equals(TutorialCheckpoint? other) => other != null && Identity.Equals(other.Identity) &&
            string.Equals(StepKey, other.StepKey, StringComparison.Ordinal) && State == other.State && Revision == other.Revision;
        public override bool Equals(object? obj) => Equals(obj as TutorialCheckpoint);
        public override int GetHashCode() => HashCode.Combine(Identity, StepKey, (int)State, Revision);
    }

    /// <summary>Skipped tutorials remain incomplete; only an acknowledged Completed checkpoint completes one.</summary>
    public enum TutorialCheckpointState { InProgress, Completed, Skipped }
}
