using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Entitlements
{

    /// <summary>Presentation projection. No local clock is accepted because device time cannot create authority.</summary>
    public sealed class EntitlementSnapshot
    {
        private readonly EntitlementRightView[] rights;

        public EntitlementSnapshot(EntitlementConfirmedSnapshot confirmed, IEnumerable<EntitlementRightView> rights)
        {
            Confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed));
            if (rights == null)
                throw new ArgumentNullException(nameof(rights));
            this.rights = rights.ToArray();
            if (this.rights.Length != confirmed.Rights.Count || this.rights.Any(value => value == null) ||
                this.rights.Select(value => value.Right.EntitlementId).Distinct().Count() != this.rights.Length)
                throw new ArgumentException("Entitlement views do not faithfully describe the confirmed projection.", nameof(rights));
        }

        public EntitlementConfirmedSnapshot Confirmed { get; }
        public IReadOnlyList<EntitlementRightView> Rights => Array.AsReadOnly(rights);
    }
}
