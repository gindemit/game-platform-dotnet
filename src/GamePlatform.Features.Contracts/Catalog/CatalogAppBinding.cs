using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Catalog
{

    /// <summary>App-specific binding data. It never alters the canonical definition.</summary>
    public sealed class CatalogAppBinding
    {
        private const int MaximumPresentationPayloadBytes = 16 * 1024;
        private readonly byte[] presentationPayload;

        public CatalogAppBinding(PlatformId definitionId, long bindingVersion, bool visible,
            bool canUse, bool canGrant, bool canSpend, bool canSubmit, ReadOnlySpan<byte> presentationPayload)
        {
            if (!definitionId.IsValid)
                throw new ArgumentException("A stable definition ID is required.", nameof(definitionId));
            if (bindingVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(bindingVersion));
            if (presentationPayload.Length > MaximumPresentationPayloadBytes)
                throw new ArgumentOutOfRangeException(nameof(presentationPayload));
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
            if (!Visible)
                return false;
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
}
