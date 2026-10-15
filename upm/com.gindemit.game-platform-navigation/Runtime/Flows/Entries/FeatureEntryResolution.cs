namespace GamePlatform.Navigation.Kernel.Flows
{
    public enum FeatureEntryResolutionKind { Admitted, JoinedPendingAdmission, ReusedCurrent }

    /// <summary>The flow handle returned by an injected navigation adapter.</summary>
    public sealed class FeatureEntryResolution<THandle> where THandle : class
    {
        public FeatureEntryResolution(FeatureEntryResolutionKind kind, THandle handle)
        { Kind = kind; Handle = handle; }
        public FeatureEntryResolutionKind Kind { get; }
        public THandle Handle { get; }
    }
}
