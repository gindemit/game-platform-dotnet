using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Catalog
{

    /// <summary>Exact cache identity. Owner includes backend, app, account, view and generation.</summary>
    public sealed class CatalogSnapshotRequest
    {
        public CatalogSnapshotRequest(ScopedOwnerContext owner, long snapshotVersion, string locale, string audience)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (snapshotVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(snapshotVersion));
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
}
