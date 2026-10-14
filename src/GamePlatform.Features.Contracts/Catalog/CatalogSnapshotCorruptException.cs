using System;

namespace GamePlatform.Features.Contracts.Catalog
{

    public sealed class CatalogSnapshotCorruptException : Exception
    {
        public CatalogSnapshotCorruptException(string message, Exception? innerException = null) : base(message, innerException) { }
    }
}
