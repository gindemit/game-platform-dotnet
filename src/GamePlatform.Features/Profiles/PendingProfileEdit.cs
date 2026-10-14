using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{

    /// <summary>Durable local intent, never a claim that the server accepted the profile fields.</summary>
    public sealed class PendingProfileEdit
    {
        public PendingProfileEdit(OperationId operationId, ClientStreamId streamId, string businessSource, ProfilePatch patch,
            long localRevision, ProfilePendingStatus status, long? acceptedRevision)
        {
            if (!operationId.IsValid)
                throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (!streamId.IsValid)
                throw new ArgumentException("A valid stream identity is required.", nameof(streamId));
            ProfileValidation.BusinessSource(businessSource, nameof(businessSource));
            Patch = patch ?? throw new ArgumentNullException(nameof(patch));
            if (localRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(localRevision));
            if (!Enum.IsDefined(typeof(ProfilePendingStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (status == ProfilePendingStatus.AcceptedAwaitingPull && (!acceptedRevision.HasValue || acceptedRevision.Value <= patch.ExpectedRevision))
                throw new ArgumentException("An accepted pending edit requires a newer confirmed revision.", nameof(acceptedRevision));
            if (status != ProfilePendingStatus.AcceptedAwaitingPull && acceptedRevision.HasValue)
                throw new ArgumentException("Only an accepted pending edit carries an accepted revision.", nameof(acceptedRevision));
            OperationId = operationId;
            StreamId = streamId;
            BusinessSource = businessSource;
            LocalRevision = localRevision;
            Patch = patch;
            Status = status;
            AcceptedRevision = acceptedRevision;
        }

        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public ProfilePatch Patch { get; }
        public long LocalRevision { get; }
        public ProfilePendingStatus Status { get; }
        public long? AcceptedRevision { get; }
    }
}
