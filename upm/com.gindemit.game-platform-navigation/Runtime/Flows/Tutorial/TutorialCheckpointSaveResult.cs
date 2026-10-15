using System;

namespace GamePlatform.Navigation.Kernel.Flows
{
    /// <summary>Explicit store acknowledgement. The returned checkpoint must exactly match the requested write.</summary>
    public sealed class TutorialCheckpointSaveResult
    {
        private TutorialCheckpointSaveResult(bool acknowledged, TutorialCheckpoint? checkpoint)
        { IsAcknowledged = acknowledged; Checkpoint = checkpoint; }

        public bool IsAcknowledged { get; }
        public TutorialCheckpoint? Checkpoint { get; }
        public static TutorialCheckpointSaveResult Acknowledge(TutorialCheckpoint checkpoint) =>
            new TutorialCheckpointSaveResult(true, checkpoint ?? throw new ArgumentNullException(nameof(checkpoint)));
        public static TutorialCheckpointSaveResult Reject() => new TutorialCheckpointSaveResult(false, null);
    }
}
