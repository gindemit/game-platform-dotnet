using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public sealed class BootstrapBoundary
    {
        private readonly byte[] session, firstPage; public BootstrapBoundary(ClientStreamId streamId, string streamState, long finalizedThrough, long? nextSequence, long committedThrough, long visibilityGeneration, Guid logEpoch, long expiresAt, IReadOnlyList<string> requiredCollections, byte[] session, byte[] firstPage)
        {
            StreamId = streamId;
            StreamState = streamState ?? throw new ArgumentNullException(nameof(streamState));
            FinalizedThrough = finalizedThrough;
            NextSequence = nextSequence;
            CommittedThrough = committedThrough;
            VisibilityGeneration = visibilityGeneration;
            LogEpoch = logEpoch;
            ExpiresAt = expiresAt;
            RequiredCollections = Copy(requiredCollections);
            this.session = (byte[])session.Clone();
            this.firstPage = (byte[])firstPage.Clone();
        }
        public ClientStreamId StreamId { get; }
        public string StreamState { get; }
        public long FinalizedThrough { get; }
        public long? NextSequence { get; }
        public long CommittedThrough { get; }
        public long VisibilityGeneration { get; }
        public Guid LogEpoch { get; }
        public long ExpiresAt { get; }
        public IReadOnlyList<string> RequiredCollections { get; }
        public byte[] CopySession() => (byte[])session.Clone(); public byte[] CopyFirstPageToken() => (byte[])firstPage.Clone();
        private static IReadOnlyList<string> Copy(IReadOnlyList<string> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            var copy = new string[values.Count];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = values[i] ?? throw new ArgumentException("A collection is null.", nameof(values));
            return Array.AsReadOnly(copy);
        }
    }
}
