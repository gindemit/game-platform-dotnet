namespace GamePlatform.Storage.Sqlite.Outbox
{

    public enum StreamRecoveryState
    {
        Current, LocalGap, ServerAheadOfLocalState
    }
}
