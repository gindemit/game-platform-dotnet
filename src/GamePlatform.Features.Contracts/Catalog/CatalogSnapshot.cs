using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Catalog
{

    /// <summary>An immutable, validated allowed-definition snapshot; it is not a grant or spend authority.</summary>
    public sealed class CatalogSnapshot
    {
        private const int MaximumEntries = 512;
        private const int MaximumTotalPayloadBytes = 192 * 1024;
        private readonly CatalogEntry[] entries;

        public CatalogSnapshot(CatalogSnapshotRequest request, int schemaVersion, long revision,
            long confirmedAtMilliseconds, IEnumerable<CatalogEntry> entries)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            if (schemaVersion != 1)
                throw new CatalogSnapshotUnsupportedVersionException("snapshot-schema", schemaVersion);
            if (revision <= 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            if (confirmedAtMilliseconds < 0 || confirmedAtMilliseconds > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(confirmedAtMilliseconds));
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            this.entries = entries.ToArray();
            if (this.entries.Length > MaximumEntries)
                throw new CatalogSnapshotCorruptException("The catalog snapshot exceeds its entry bound.");
            var definitionIds = new HashSet<PlatformId>();
            var semanticKeys = new HashSet<SemanticId>();
            var totalPayload = 0;
            foreach (var entry in this.entries)
            {
                if (entry == null)
                    throw new CatalogSnapshotCorruptException("The catalog snapshot contains an incomplete entry.");
                if (!definitionIds.Add(entry.Definition.DefinitionId) || !semanticKeys.Add(entry.Definition.SemanticKey))
                    throw new CatalogSnapshotCorruptException("The catalog snapshot contains duplicate definition identities.");
                totalPayload = checked(totalPayload + entry.Definition.DefinitionPayloadLength + entry.Binding.PresentationPayloadLength);
                if (totalPayload > MaximumTotalPayloadBytes)
                    throw new CatalogSnapshotCorruptException("The catalog snapshot exceeds its payload bound.");
            }
            SchemaVersion = schemaVersion;
            Revision = revision;
            ConfirmedAtMilliseconds = confirmedAtMilliseconds;
        }

        public CatalogSnapshotRequest Request { get; }
        public int SchemaVersion { get; }
        public long Revision { get; }
        public long ConfirmedAtMilliseconds { get; }
        public IReadOnlyList<CatalogEntry> Entries => Array.AsReadOnly(entries);
    }
}
