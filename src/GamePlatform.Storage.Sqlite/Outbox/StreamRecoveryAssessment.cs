using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Executor;

namespace GamePlatform.Storage.Sqlite.Outbox
{

    public readonly struct StreamRecoveryAssessment
    {
        public StreamRecoveryAssessment(StreamRecoveryState state, long localNextSequence, long serverFinalizedThrough)
        {
            State = state;
            LocalNextSequence = localNextSequence;
            ServerFinalizedThrough = serverFinalizedThrough;
        }
        public StreamRecoveryState State { get; }
        public long LocalNextSequence { get; }
        public long ServerFinalizedThrough { get; }
    }
}
