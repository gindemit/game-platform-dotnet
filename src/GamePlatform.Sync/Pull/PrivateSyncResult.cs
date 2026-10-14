namespace GamePlatform.Sync.Pull
{
    public enum PrivateSyncResult
    {
        BootstrapInstalled,
        PageApplied,
        BoundaryComplete,
        ResetReconciled,
        RemoteFailure,
        StreamRecovering
    }
}
