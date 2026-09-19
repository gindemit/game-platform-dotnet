using System;

namespace GamePlatform.Features.Contracts
{
    // Legacy values are preserved. This contract provides no client permission to mutate server-owned state.
    public enum DataOwnership { LocalPreference = 0, ValidatedProgress = 1, ServerEconomy = 2, ServerSocial = 3, MergedProfile = 4 }

    public sealed class StateRecord
    {
        public StateRecord(int schemaVersion, long revision, string payload, DataOwnership ownership)
        {
            if (schemaVersion < 1 || schemaVersion > 1000000) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!Enum.IsDefined(typeof(DataOwnership), ownership)) throw new ArgumentOutOfRangeException(nameof(ownership));
            SchemaVersion = schemaVersion; Revision = revision; Ownership = ownership;
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            Payload = CompatibilityGuard.Optional(payload, nameof(payload), 1024 * 1024);
        }
        public int SchemaVersion { get; }
        public long Revision { get; }
        public string Payload { get; }
        public DataOwnership Ownership { get; }
    }
}
