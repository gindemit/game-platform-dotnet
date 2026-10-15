namespace GamePlatform.Navigation.Kernel.Flows
{
    public sealed class TutorialFlowResult
    {
        public TutorialFlowResult(TutorialFlowOutcome outcome, TutorialCheckpoint? checkpoint)
        { Outcome = outcome; Checkpoint = checkpoint; }
        public TutorialFlowOutcome Outcome { get; }
        public TutorialCheckpoint? Checkpoint { get; }
    }
}
