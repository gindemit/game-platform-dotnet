#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Catalog
{
    /// <summary>Server-owned resource kinds supported by the bounded value slice.</summary>
    public enum CatalogResourceKind { Currency, Item, Entitlement, Reward }

    /// <summary>Visibility is deliberately separate from every authority permission.</summary>
    public enum CatalogPermission { Visible, Use, Grant, Spend, Submit }

    public sealed class CatalogDefinition
    {
        private const int MaximumPayloadBytes = 16 * 1024;
        private readonly byte[] definitionPayload;

        public CatalogDefinition(PlatformId definitionId, CatalogResourceKind kind, SemanticId semanticKey,
            long definitionVersion, int schemaVersion, ReadOnlySpan<byte> definitionPayload)
        {
            if (!definitionId.IsValid) throw new ArgumentException("A stable definition ID is required.", nameof(definitionId));
            if (!semanticKey.IsValid) throw new ArgumentException("A semantic key is required.", nameof(semanticKey));
            if (!Enum.IsDefined(typeof(CatalogResourceKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (definitionVersion <= 0) throw new ArgumentOutOfRangeException(nameof(definitionVersion));
            if (schemaVersion != 1) throw new CatalogSnapshotUnsupportedVersionException("definition-schema", schemaVersion);
            if (definitionPayload.Length > MaximumPayloadBytes) throw new ArgumentOutOfRangeException(nameof(definitionPayload));
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

    /// <summary>App-specific binding data. It never alters the canonical definition.</summary>
    public sealed class CatalogAppBinding
    {
        private const int MaximumPresentationPayloadBytes = 16 * 1024;
        private readonly byte[] presentationPayload;

        public CatalogAppBinding(PlatformId definitionId, long bindingVersion, bool visible,
            bool canUse, bool canGrant, bool canSpend, bool canSubmit, ReadOnlySpan<byte> presentationPayload)
        {
            if (!definitionId.IsValid) throw new ArgumentException("A stable definition ID is required.", nameof(definitionId));
            if (bindingVersion <= 0) throw new ArgumentOutOfRangeException(nameof(bindingVersion));
            if (presentationPayload.Length > MaximumPresentationPayloadBytes) throw new ArgumentOutOfRangeException(nameof(presentationPayload));
            DefinitionId = definitionId;
            BindingVersion = bindingVersion;
            Visible = visible;
            CanUse = canUse;
            CanGrant = canGrant;
            CanSpend = canSpend;
            CanSubmit = canSubmit;
            this.presentationPayload = presentationPayload.ToArray();
        }

        public PlatformId DefinitionId { get; }
        public long BindingVersion { get; }
        public bool Visible { get; }
        public bool CanUse { get; }
        public bool CanGrant { get; }
        public bool CanSpend { get; }
        public bool CanSubmit { get; }
        public int PresentationPayloadLength => presentationPayload.Length;
        public byte[] CopyPresentationPayload() => (byte[])presentationPayload.Clone();

        public bool Permits(CatalogResourceKind kind, CatalogPermission permission)
        {
            if (!Visible) return false;
            return permission switch
            {
                CatalogPermission.Visible => true,
                CatalogPermission.Use => (kind == CatalogResourceKind.Item || kind == CatalogResourceKind.Entitlement) && CanUse,
                CatalogPermission.Grant => CanGrant,
                CatalogPermission.Spend => kind == CatalogResourceKind.Currency && CanSpend,
                CatalogPermission.Submit => kind == CatalogResourceKind.Reward && CanSubmit,
                _ => false
            };
        }
    }

    public sealed class CatalogEntry
    {
        public CatalogEntry(CatalogDefinition definition, CatalogAppBinding binding)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            if (!definition.DefinitionId.Equals(binding.DefinitionId)) throw new ArgumentException("The app binding targets another definition.", nameof(binding));
            if (!binding.Visible) throw new CatalogSnapshotCorruptException("Invisible bindings must not be installed in an allowed catalog snapshot.");
        }

        public CatalogDefinition Definition { get; }
        public CatalogAppBinding Binding { get; }
    }

    /// <summary>Exact cache identity. Owner includes backend, app, account, view and generation.</summary>
    public sealed class CatalogSnapshotRequest
    {
        public CatalogSnapshotRequest(ScopedOwnerContext owner, long snapshotVersion, string locale, string audience)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (snapshotVersion <= 0) throw new ArgumentOutOfRangeException(nameof(snapshotVersion));
            ValidateDimension(locale, 64, nameof(locale));
            ValidateDimension(audience, 128, nameof(audience));
            Owner = owner;
            SnapshotVersion = snapshotVersion;
            Locale = locale;
            Audience = audience;
        }

        public ScopedOwnerContext Owner { get; }
        public long SnapshotVersion { get; }
        public string Locale { get; }
        public string Audience { get; }

        public string ToCanonicalQuery() => "version=" + SnapshotVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ";locale=" + Locale + ";audience=" + Audience;

        private static void ValidateDimension(string value, int maximum, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentOutOfRangeException(parameter);
        }
    }

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
            if (schemaVersion != 1) throw new CatalogSnapshotUnsupportedVersionException("snapshot-schema", schemaVersion);
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (confirmedAtMilliseconds < 0 || confirmedAtMilliseconds > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(confirmedAtMilliseconds));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            this.entries = entries.ToArray();
            if (this.entries.Length > MaximumEntries) throw new CatalogSnapshotCorruptException("The catalog snapshot exceeds its entry bound.");
            var definitionIds = new HashSet<PlatformId>();
            var semanticKeys = new HashSet<SemanticId>();
            var totalPayload = 0;
            foreach (var entry in this.entries)
            {
                if (entry == null) throw new CatalogSnapshotCorruptException("The catalog snapshot contains an incomplete entry.");
                if (!definitionIds.Add(entry.Definition.DefinitionId) || !semanticKeys.Add(entry.Definition.SemanticKey))
                    throw new CatalogSnapshotCorruptException("The catalog snapshot contains duplicate definition identities.");
                totalPayload = checked(totalPayload + entry.Definition.DefinitionPayloadLength + entry.Binding.PresentationPayloadLength);
                if (totalPayload > MaximumTotalPayloadBytes) throw new CatalogSnapshotCorruptException("The catalog snapshot exceeds its payload bound.");
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

    public sealed class CatalogSnapshotUnsupportedVersionException : Exception
    {
        public CatalogSnapshotUnsupportedVersionException(string boundary, int version)
            : base("Unsupported catalog " + boundary + " version " + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".")
        { Boundary = boundary; Version = version; }
        public string Boundary { get; }
        public int Version { get; }
    }

    public sealed class CatalogSnapshotCorruptException : Exception
    {
        public CatalogSnapshotCorruptException(string message, Exception? innerException = null) : base(message, innerException) { }
    }
}
