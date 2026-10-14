namespace GamePlatform.Backend.Contracts.Remote
{
    public enum CommandStreamRecoveryReason
    {
        LocalDatabaseRecreated,
        BackupRestored,
        StreamIdentityConflict
    }
}
