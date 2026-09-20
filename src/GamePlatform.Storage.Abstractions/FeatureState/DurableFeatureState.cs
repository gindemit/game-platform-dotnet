using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.FeatureState
{
    public sealed class DurableFeatureState
    {
        private readonly byte[] payload;
        private readonly byte[] extensions;

        public DurableFeatureState(ScopedOwnerContext owner, string featureNamespace, string entityKey,
            long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions)
        {
            ValidateIdentity(owner, featureNamespace, entityKey);
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (confirmedAtMilliseconds < 0 || confirmedAtMilliseconds > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(confirmedAtMilliseconds));
            if (payload.Length > 262_144) throw new ArgumentOutOfRangeException(nameof(payload));
            if (extensions.Length > 65_536) throw new ArgumentOutOfRangeException(nameof(extensions));
            Owner = owner; FeatureNamespace = featureNamespace; EntityKey = entityKey; Revision = revision;
            ConfirmedAtMilliseconds = confirmedAtMilliseconds;
            this.payload = payload.ToArray(); this.extensions = extensions.ToArray();
        }

        public ScopedOwnerContext Owner { get; }
        public string FeatureNamespace { get; }
        public string EntityKey { get; }
        public long Revision { get; }
        public long ConfirmedAtMilliseconds { get; }
        public int PayloadLength => payload.Length;
        public int ExtensionLength => extensions.Length;
        public byte[] CopyPayload() => (byte[])payload.Clone();
        public byte[] CopyExtensions() => (byte[])extensions.Clone();

        internal static void ValidateIdentity(ScopedOwnerContext owner, string featureNamespace, string entityKey)
        {
            if (!owner.IsValid) throw new ArgumentException("A valid owner is required.", nameof(owner));
            ValidateText(featureNamespace, 128, nameof(featureNamespace));
            ValidateText(entityKey, 256, nameof(entityKey));
        }

        private static void ValidateText(string value, int maximum, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    public sealed class DurableFeatureMutation
    {
        private readonly byte[] payload;
        private readonly byte[]? extensions;

        public DurableFeatureMutation(ScopedOwnerContext owner, string featureNamespace, string entityKey,
            long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, byte[]? extensions)
        {
            DurableFeatureState.ValidateIdentity(owner, featureNamespace, entityKey);
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (confirmedAtMilliseconds < 0 || confirmedAtMilliseconds > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(confirmedAtMilliseconds));
            if (payload.Length > 262_144) throw new ArgumentOutOfRangeException(nameof(payload));
            if (extensions != null && extensions.Length > 65_536) throw new ArgumentOutOfRangeException(nameof(extensions));
            Owner = owner; FeatureNamespace = featureNamespace; EntityKey = entityKey; Revision = revision;
            ConfirmedAtMilliseconds = confirmedAtMilliseconds; this.payload = payload.ToArray();
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

    public interface IDurableFeatureStateStore
    {
        Task<DurableFeatureState?> ReadAsync(ScopedOwnerContext owner, string featureNamespace, string entityKey, CancellationToken cancellationToken);
        void Upsert(ILocalStorageTransaction transaction, DurableFeatureMutation mutation);
    }
}
