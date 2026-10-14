using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    /// <summary>Immutable recovery identity; persist this whole request before the first send.</summary>
    public sealed class CommandStreamRecoveryRequest
    {
        public CommandStreamRecoveryRequest(ClientStreamId priorStreamId, ClientStreamId newStreamId,
            Guid recoveryId, CommandStreamRecoveryReason reason, string inspectionToken, long expectedFinalizedThrough)
        {
            if (!priorStreamId.IsValid || !newStreamId.IsValid || priorStreamId == newStreamId)
                throw new ArgumentException("Distinct valid stream identities are required.");
            if (!UuidIdentity.IsValid(newStreamId.Value, false) || !UuidIdentity.IsValid(recoveryId, false))
                throw new ArgumentException("New stream and recovery identities must be UUIDv7.");
            if (!Enum.IsDefined(typeof(CommandStreamRecoveryReason), reason))
                throw new ArgumentOutOfRangeException(nameof(reason));
            CommandStreamInspection.ValidateToken(inspectionToken);
            if (expectedFinalizedThrough < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedFinalizedThrough));
            PriorStreamId = priorStreamId;
            NewStreamId = newStreamId;
            RecoveryId = recoveryId;
            Reason = reason;
            InspectionToken = inspectionToken;
            ExpectedFinalizedThrough = expectedFinalizedThrough;
        }

        public ClientStreamId PriorStreamId { get; }
        public ClientStreamId NewStreamId { get; }
        public Guid RecoveryId { get; }
        public CommandStreamRecoveryReason Reason { get; }
        public string InspectionToken { get; }
        public long ExpectedFinalizedThrough { get; }
    }
}
