namespace GamePlatform.Navigation.Kernel.Flows
{
    /// <summary>Explicit conditions that defer automatic prompts.</summary>
    public readonly struct PromptSchedulingContext
    {
        public PromptSchedulingContext(bool manualNavigationActive, bool tutorialActive, bool recoveryBlocked)
        { ManualNavigationActive = manualNavigationActive; TutorialActive = tutorialActive; RecoveryBlocked = recoveryBlocked; }
        public bool ManualNavigationActive { get; }
        public bool TutorialActive { get; }
        public bool RecoveryBlocked { get; }
        public bool DefersPrompts => ManualNavigationActive || TutorialActive || RecoveryBlocked;
    }
}
