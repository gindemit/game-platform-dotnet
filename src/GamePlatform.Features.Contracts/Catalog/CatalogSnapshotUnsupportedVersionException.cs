using System;

namespace GamePlatform.Features.Contracts.Catalog
{

    public sealed class CatalogSnapshotUnsupportedVersionException : Exception
    {
        public CatalogSnapshotUnsupportedVersionException(string boundary, int version)
            : base("Unsupported catalog " + boundary + " version " + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".")
        {
            Boundary = boundary;
            Version = version;
        }
        public string Boundary { get; }
        public int Version { get; }
    }
}
