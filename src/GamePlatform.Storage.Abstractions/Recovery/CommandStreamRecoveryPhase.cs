namespace GamePlatform.Storage.Abstractions.Recovery
{
    public enum CommandStreamRecoveryPhase
    {
        Expired = 0, Prepared = 1, RemoteCommitted = 2, Adopted = 3, BootstrapComplete = 4
    }
}
