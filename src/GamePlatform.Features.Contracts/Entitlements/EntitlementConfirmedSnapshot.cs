using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Entitlements
{

    /// <summary>Bounded immutable server projection. Only the server may call a right expired or revoked.</summary>
    public sealed class EntitlementConfirmedSnapshot
    {
        private readonly EntitlementRight[] rights;

        public EntitlementConfirmedSnapshot(long revision, long confirmedAtMilliseconds, IEnumerable<EntitlementRight> rights)
        {
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            if (confirmedAtMilliseconds < 0 || confirmedAtMilliseconds > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(confirmedAtMilliseconds));
            if (rights == null)
                throw new ArgumentNullException(nameof(rights));
            this.rights = rights.ToArray();
            if (this.rights.Length > 512 || this.rights.Any(value => value == null) || this.rights.Select(value => value.EntitlementId).Distinct().Count() != this.rights.Length)
                throw new ArgumentException("Entitlement projection contains invalid or duplicate rights.", nameof(rights));
            Revision = revision;
            ConfirmedAtMilliseconds = confirmedAtMilliseconds;
        }

        public long Revision { get; }
        public long ConfirmedAtMilliseconds { get; }
        public IReadOnlyList<EntitlementRight> Rights => Array.AsReadOnly(rights);
    }
}
