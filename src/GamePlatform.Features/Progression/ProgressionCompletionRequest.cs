using System;
using GamePlatform.Core;
using GamePlatform.Features.Contracts;

namespace GamePlatform.Features.Progression
{

    /// <summary>Game-owned completion data presented to the portable command boundary. This type allocates no progress or reward.</summary>
    public sealed class ProgressionCompletionRequest
    {
        public ProgressionCompletionRequest(ScopedOwnerContext owner, OperationId operationId, ClientStreamId streamId,
            string businessSource, GameplayOutcome outcome, int contentVersion, ProgressionOutcomeAuthority outcomeAuthority,
            string validationScheme = "untrusted-reference", ProgressionCommandExtension? extension = null)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("A valid operation ID is required.", nameof(operationId));
            if (!streamId.IsValid)
                throw new ArgumentException("A valid stream ID is required.", nameof(streamId));
            ProgressionValidation.BusinessSource(businessSource, nameof(businessSource));
            if (outcome == null)
                throw new ArgumentNullException(nameof(outcome));
            if (contentVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(contentVersion));
            if (!Enum.IsDefined(typeof(ProgressionOutcomeAuthority), outcomeAuthority))
                throw new ArgumentOutOfRangeException(nameof(outcomeAuthority));
            ProgressionValidation.Semantic(validationScheme, nameof(validationScheme));
            Owner = owner;
            OperationId = operationId;
            StreamId = streamId;
            BusinessSource = businessSource;
            Outcome = outcome;
            ContentVersion = contentVersion;
            OutcomeAuthority = outcomeAuthority;
            ValidationScheme = validationScheme;
            Extension = extension;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public GameplayOutcome Outcome { get; }
        public int ContentVersion { get; }
        public ProgressionOutcomeAuthority OutcomeAuthority { get; }
        public string ValidationScheme { get; }
        public ProgressionCommandExtension? Extension { get; }
    }
}
