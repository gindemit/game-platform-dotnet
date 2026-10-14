using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;

namespace GamePlatform.Features.Progression
{

    public sealed class ProgressionConfirmationEvidenceHead
    {
        public ProgressionConfirmationEvidenceHead(long? latestRevision,
            IReadOnlyList<ProgressionConfirmationEvidence> activeGroups)
        {
            if (latestRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(latestRevision));
            LatestRevision = latestRevision;
            ActiveGroups = activeGroups ?? throw new ArgumentNullException(nameof(activeGroups));
        }
        public long? LatestRevision { get; }
        public IReadOnlyList<ProgressionConfirmationEvidence> ActiveGroups { get; }
    }
}
