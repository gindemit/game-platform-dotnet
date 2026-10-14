namespace GamePlatform.Sync.Recovery
{
    public enum CommandStreamRecoveryOutcome
    {
        BootstrapComplete, DamageNotEstablished, UnresolvedCommands, RemoteFailure,
        OutcomeUncertain, BootstrapRequired, Retired
    }
}
