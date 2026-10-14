using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.FeatureState
{

    public sealed class DurableFeatureMutation
    {
        private readonly byte[] payload;
        private readonly byte[]? extensions;

        public DurableFeatureMutation(ScopedOwnerContext owner, string featureNamespace, string entityKey,
            long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, byte[]? extensions)
        {
            DurableFeatureState.ValidateIdentity(owner, featureNamespace, entityKey);
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            if (confirmedAtMilliseconds < 0 || confirmedAtMilliseconds > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(confirmedAtMilliseconds));
            if (payload.Length > 262_144)
                throw new ArgumentOutOfRangeException(nameof(payload));
            if (extensions != null && extensions.Length > 65_536)
                throw new ArgumentOutOfRangeException(nameof(extensions));
            Owner = owner;
            FeatureNamespace = featureNamespace;
            EntityKey = entityKey;
            Revision = revision;
            ConfirmedAtMilliseconds = confirmedAtMilliseconds;
            this.payload = payload.ToArray();
            this.extensions = extensions == null ? null : (byte[])extensions.Clone();
        }

        public ScopedOwnerContext Owner { get; }
        public string FeatureNamespace { get; }
        public string EntityKey { get; }
        public long Revision { get; }
        public long ConfirmedAtMilliseconds { get; }
        public bool PreservesExistingExtensions => extensions == null;
        public byte[] CopyPayload() => (byte[])payload.Clone();
        public byte[]? CopyExtensions() => extensions == null ? null : (byte[])extensions.Clone();
    }
}
