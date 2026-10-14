using System;
using System.Collections.Generic;

namespace GamePlatform.Backend.Contracts.Remote
{

    public sealed class RemotePullGroup
    {
        public RemotePullGroup(long feedRevision, IReadOnlyList<RemoteProjectionMutation> changes)
        {
            if (feedRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(feedRevision));
            if (changes == null || changes.Count == 0)
                throw new ArgumentOutOfRangeException(nameof(changes));
            FeedRevision = feedRevision;
            var copy = new RemoteProjectionMutation[changes.Count];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = changes[i] ?? throw new ArgumentException("A change is null.", nameof(changes));
            Changes = Array.AsReadOnly(copy);
        }
        public long FeedRevision { get; }
        public IReadOnlyList<RemoteProjectionMutation> Changes { get; }
    }
}
