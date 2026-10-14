using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Storage.Abstractions.Sync
{
    public sealed class StoredProjectionMutation
    {
        private readonly byte[] payload;
        public StoredProjectionMutation(string collection, string entityKey, long revision, StoredProjectionKind kind, byte[] payload)
        {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
            EntityKey = entityKey ?? throw new ArgumentNullException(nameof(entityKey));
            Revision = revision;
            Kind = kind;
            this.payload = (byte[])(payload ?? throw new ArgumentNullException(nameof(payload))).Clone();
        }
        public string Collection { get; }
        public string EntityKey { get; }
        public long Revision { get; }
        public StoredProjectionKind Kind { get; }
        public byte[] CopyPayload() => (byte[])payload.Clone();
    }
}
