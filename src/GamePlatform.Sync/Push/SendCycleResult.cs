using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Sync.Push
{
    public enum SendCycleResult
    {
        Idle, Finalized, Retryable, OutcomeUncertain
    }
}
