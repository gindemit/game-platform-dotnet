using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions.Accounts;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Abstractions.Recovery;
using JournalRequest = GamePlatform.Storage.Abstractions.Recovery.CommandStreamRecoveryRequest;
using RemoteRequest = GamePlatform.Backend.Contracts.Remote.CommandStreamRecoveryRequest;

namespace GamePlatform.Sync.Recovery
{
    public enum CommandStreamRecoveryOutcome
    {
        BootstrapComplete, DamageNotEstablished, UnresolvedCommands, RemoteFailure,
        OutcomeUncertain, BootstrapRequired, Retired
    }
}
