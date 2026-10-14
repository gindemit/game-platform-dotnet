using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public sealed class StoredTerminalCommand
    {
        public StoredTerminalCommand(LeasedCommand command, CommandTerminalOutcome outcome)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Outcome = outcome;
        }
        public LeasedCommand Command { get; }
        public CommandTerminalOutcome Outcome { get; }
    }
}
