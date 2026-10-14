using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;
using GamePlatform.Features.Contracts;

namespace GamePlatform.Features.Progression
{

    /// <summary>Durable local intent. Accepted means only that the command receipt was retained; pull remains authoritative.</summary>
    public sealed class PendingProgressionCompletion
    {
        public PendingProgressionCompletion(OperationId operationId, ClientStreamId streamId, string businessSource,
            GameplayOutcome outcome, int contentVersion, ProgressionOutcomeAuthority outcomeAuthority, long localRevision,
            ProgressionPendingStatus status, string validationScheme = "untrusted-reference",
            ProgressionCommandExtension? extension = null)
        {
            if (!operationId.IsValid || !streamId.IsValid)
                throw new ArgumentException("Valid command identities are required.");
            ProgressionValidation.BusinessSource(businessSource, nameof(businessSource));
            if (outcome == null)
                throw new ArgumentNullException(nameof(outcome));
            if (contentVersion < 1 || localRevision < 1)
                throw new ArgumentOutOfRangeException(contentVersion < 1 ? nameof(contentVersion) : nameof(localRevision));
            if (!Enum.IsDefined(typeof(ProgressionOutcomeAuthority), outcomeAuthority) || !Enum.IsDefined(typeof(ProgressionPendingStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            ProgressionValidation.Semantic(validationScheme, nameof(validationScheme));
            OperationId = operationId;
            StreamId = streamId;
            BusinessSource = businessSource;
            Outcome = outcome;
            ContentVersion = contentVersion;
            OutcomeAuthority = outcomeAuthority;
            LocalRevision = localRevision;
            Status = status;
            ValidationScheme = validationScheme;
            Extension = extension;
        }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public GameplayOutcome Outcome { get; }
        public int ContentVersion { get; }
        public ProgressionOutcomeAuthority OutcomeAuthority { get; }
        public long LocalRevision { get; }
        public ProgressionPendingStatus Status { get; }
        public string ValidationScheme { get; }
        public ProgressionCommandExtension? Extension { get; }
    }
}
