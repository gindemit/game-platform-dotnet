using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public sealed class BootstrapProgress
    {
        private readonly byte[] session, pageToken; public BootstrapProgress(ClientStreamId streamId, long committedThrough, long visibilityGeneration, Guid logEpoch, long expiresAt, byte[] session, byte[] pageToken)
        {
            StreamId = streamId;
            CommittedThrough = committedThrough;
            VisibilityGeneration = visibilityGeneration;
            LogEpoch = logEpoch;
            ExpiresAt = expiresAt;
            this.session = (byte[])session.Clone();
            this.pageToken = (byte[])pageToken.Clone();
        }
        public ClientStreamId StreamId { get; }
        public long CommittedThrough { get; }
        public long VisibilityGeneration { get; }
        public Guid LogEpoch { get; }
        public long ExpiresAt { get; }
        public byte[] CopySession() => (byte[])session.Clone(); public byte[] CopyPageToken() => (byte[])pageToken.Clone();
    }
}
