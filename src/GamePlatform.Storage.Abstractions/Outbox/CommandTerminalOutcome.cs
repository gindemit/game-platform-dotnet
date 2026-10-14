using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Outbox
{
    public enum CommandTerminalOutcome
    {
        Accepted, Rejected
    }
}
