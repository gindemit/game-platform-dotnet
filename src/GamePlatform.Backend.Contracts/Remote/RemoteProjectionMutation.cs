using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public sealed class RemoteProjectionMutation
    {
        private readonly byte[] payload;
        public RemoteProjectionMutation(string collection, string entityKey, long entityRevision, ProjectionMutationKind kind, byte[]? payload)
        {
            if (string.IsNullOrWhiteSpace(collection) || collection.Length > 64)
                throw new ArgumentOutOfRangeException(nameof(collection));
            if (string.IsNullOrWhiteSpace(entityKey) || entityKey.Length > 512)
                throw new ArgumentOutOfRangeException(nameof(entityKey));
            if (entityRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(entityRevision));
            if (!Enum.IsDefined(typeof(ProjectionMutationKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == ProjectionMutationKind.Upsert && (payload == null || payload.Length == 0))
                throw new ArgumentException("An upsert payload is required.", nameof(payload));
            if ((payload?.Length ?? 0) > 262_144)
                throw new ArgumentOutOfRangeException(nameof(payload));
            Collection = collection;
            EntityKey = entityKey;
            EntityRevision = entityRevision;
            Kind = kind;
            this.payload = payload == null ? Array.Empty<byte>() : (byte[])payload.Clone();
        }
        public string Collection { get; }
        public string EntityKey { get; }
        public long EntityRevision { get; }
        public ProjectionMutationKind Kind { get; }
        public byte[] CopyPayload() => (byte[])payload.Clone();
    }
}
