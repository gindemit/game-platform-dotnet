using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Catalog
{

    public sealed class CatalogEntry
    {
        public CatalogEntry(CatalogDefinition definition, CatalogAppBinding binding)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            if (!definition.DefinitionId.Equals(binding.DefinitionId))
                throw new ArgumentException("The app binding targets another definition.", nameof(binding));
            if (!binding.Visible)
                throw new CatalogSnapshotCorruptException("Invisible bindings must not be installed in an allowed catalog snapshot.");
        }

        public CatalogDefinition Definition { get; }
        public CatalogAppBinding Binding { get; }
    }
}
