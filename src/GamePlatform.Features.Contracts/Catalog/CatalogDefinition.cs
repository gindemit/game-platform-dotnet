using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Catalog
{

    public sealed class CatalogDefinition
    {
        private const int MaximumPayloadBytes = 16 * 1024;
        private readonly byte[] definitionPayload;

        public CatalogDefinition(PlatformId definitionId, CatalogResourceKind kind, SemanticId semanticKey,
            long definitionVersion, int schemaVersion, ReadOnlySpan<byte> definitionPayload)
        {
            if (!definitionId.IsValid)
                throw new ArgumentException("A stable definition ID is required.", nameof(definitionId));
            if (!semanticKey.IsValid)
                throw new ArgumentException("A semantic key is required.", nameof(semanticKey));
            if (!Enum.IsDefined(typeof(CatalogResourceKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (definitionVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(definitionVersion));
            if (schemaVersion != 1)
                throw new CatalogSnapshotUnsupportedVersionException("definition-schema", schemaVersion);
            if (definitionPayload.Length > MaximumPayloadBytes)
                throw new ArgumentOutOfRangeException(nameof(definitionPayload));
            DefinitionId = definitionId;
            Kind = kind;
            SemanticKey = semanticKey;
            DefinitionVersion = definitionVersion;
            SchemaVersion = schemaVersion;
            this.definitionPayload = definitionPayload.ToArray();
        }

        public PlatformId DefinitionId { get; }
        public CatalogResourceKind Kind { get; }
        public SemanticId SemanticKey { get; }
        public long DefinitionVersion { get; }
        public int SchemaVersion { get; }
        public int DefinitionPayloadLength => definitionPayload.Length;
        public byte[] CopyDefinitionPayload() => (byte[])definitionPayload.Clone();
    }
}
