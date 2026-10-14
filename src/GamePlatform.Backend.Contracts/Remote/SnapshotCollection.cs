using System;

namespace GamePlatform.Backend.Contracts.Remote
{
    public sealed class SnapshotCollection
    {
        public SnapshotCollection(string name, int schemaVersion, bool required)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 64)
                throw new ArgumentOutOfRangeException(nameof(name));
            if (schemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            Name = name;
            SchemaVersion = schemaVersion;
            Required = required;
        }
        public string Name { get; }
        public int SchemaVersion { get; }
        public bool Required { get; }
    }
}
