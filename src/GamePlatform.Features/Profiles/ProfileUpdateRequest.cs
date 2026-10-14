using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{

    public sealed class ProfileUpdateRequest
    {
        public ProfileUpdateRequest(ScopedOwnerContext owner, OperationId operationId, ClientStreamId streamId,
            string businessSource, ProfilePatch patch)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid)
                throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (!streamId.IsValid)
                throw new ArgumentException("A valid stream identity is required.", nameof(streamId));
            ProfileValidation.BusinessSource(businessSource, nameof(businessSource));
            Owner = owner;
            OperationId = operationId;
            StreamId = streamId;
            BusinessSource = businessSource;
            Patch = patch ?? throw new ArgumentNullException(nameof(patch));
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public ProfilePatch Patch { get; }
    }
}
