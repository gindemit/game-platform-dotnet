using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Catalog
{

    public sealed class CatalogSnapshotCorruptException : Exception
    {
        public CatalogSnapshotCorruptException(string message, Exception? innerException = null) : base(message, innerException) { }
    }
}
