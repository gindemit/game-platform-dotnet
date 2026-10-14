#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{
    public enum CommandStreamRecoveryReason
    {
        LocalDatabaseRecreated,
        BackupRestored,
        StreamIdentityConflict
    }

    public enum CommandStreamState
    {
        Active, Retired
    }

    /// <summary>An account-owned observation. The token is opaque and must remain private.</summary>
    public sealed class CommandStreamInspection
    {
        public CommandStreamInspection(ClientStreamId streamId, Guid installationId, long finalizedThrough,
            long? nextSequence, CommandStreamState state, string inspectionToken, long expiresAt, long serverTime)
        {
            if (!streamId.IsValid || !UuidIdentity.IsValid(installationId))
                throw new ArgumentException("Valid stream identities are required.");
            if (finalizedThrough < 0 || (nextSequence.HasValue && nextSequence.Value <= 0))
                throw new ArgumentOutOfRangeException(nameof(finalizedThrough));
            if (!Enum.IsDefined(typeof(CommandStreamState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            ValidateToken(inspectionToken);
            PlatformNumbers.UnixMilliseconds(expiresAt);
            PlatformNumbers.UnixMilliseconds(serverTime);
            StreamId = streamId;
            InstallationId = installationId;
            FinalizedThrough = finalizedThrough;
            NextSequence = nextSequence;
            State = state;
            InspectionToken = inspectionToken;
            ExpiresAt = expiresAt;
            ServerTime = serverTime;
        }

        public ClientStreamId StreamId
        {
            get;
        }
        public Guid InstallationId
        {
            get;
        }
        public long FinalizedThrough
        {
            get;
        }
        public long? NextSequence
        {
            get;
        }
        public CommandStreamState State
        {
            get;
        }
        public string InspectionToken
        {
            get;
        }
        public long ExpiresAt
        {
            get;
        }
        public long ServerTime
        {
            get;
        }

        internal static void ValidateToken(string token)
        {
            if (token == null || token.Length < 16 || token.Length > 4096)
                throw new ArgumentException("A bounded inspection token is required.", nameof(token));
            foreach (var character in token)
                if (!((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z') ||
                      (character >= '0' && character <= '9') || character == '-' || character == '_'))
                    throw new ArgumentException("The inspection token is invalid.", nameof(token));
        }
    }

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

        public ClientStreamId PriorStreamId
        {
            get;
        }
        public ClientStreamId NewStreamId
        {
            get;
        }
        public Guid RecoveryId
        {
            get;
        }
        public CommandStreamRecoveryReason Reason
        {
            get;
        }
        public string InspectionToken
        {
            get;
        }
        public long ExpectedFinalizedThrough
        {
            get;
        }
    }

    public sealed class CommandStreamRecoveryResult
    {
        public CommandStreamRecoveryResult(ClientStreamId priorStreamId, ClientStreamId newStreamId,
            Guid recoveryId, long priorFinalizedThrough, long nextSequence, long serverTime)
        {
            if (!priorStreamId.IsValid || !newStreamId.IsValid || priorStreamId == newStreamId || !UuidIdentity.IsValid(recoveryId))
                throw new ArgumentException("Valid recovery identities are required.");
            if (priorFinalizedThrough < 0 || nextSequence != 1)
                throw new ArgumentOutOfRangeException(nameof(nextSequence));
            PlatformNumbers.UnixMilliseconds(serverTime);
            PriorStreamId = priorStreamId;
            NewStreamId = newStreamId;
            RecoveryId = recoveryId;
            PriorFinalizedThrough = priorFinalizedThrough;
            NextSequence = nextSequence;
            ServerTime = serverTime;
        }

        public ClientStreamId PriorStreamId
        {
            get;
        }
        public ClientStreamId NewStreamId
        {
            get;
        }
        public Guid RecoveryId
        {
            get;
        }
        public long PriorFinalizedThrough
        {
            get;
        }
        public long NextSequence
        {
            get;
        }
        public long ServerTime
        {
            get;
        }
    }

    public interface ICommandStreamRecoveryRemote
    {
        Task<RemoteResult<CommandStreamInspection>> InspectAsync(ClientStreamId streamId, CancellationToken cancellationToken);
        Task<RemoteResult<CommandStreamRecoveryResult>> RecoverAsync(CommandStreamRecoveryRequest request, CancellationToken cancellationToken);
    }
}
