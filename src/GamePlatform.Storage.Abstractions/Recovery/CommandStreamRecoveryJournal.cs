#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions.Accounts;

namespace GamePlatform.Storage.Abstractions.Recovery
{
    public enum CommandStreamRecoveryPhase { Expired = 0, Prepared = 1, RemoteCommitted = 2, Adopted = 3, BootstrapComplete = 4 }

    /// <summary>Immutable owner and request identity persisted before stream recovery is submitted.</summary>
    public sealed class CommandStreamRecoveryRequest
    {
        public CommandStreamRecoveryRequest(AccountPrincipalDescriptor principal, AppId appId, PlatformUserId accountId,
            Guid installationId, ClientStreamId priorStreamId, ClientStreamId successorStreamId, Guid recoveryId,
            string reason, string inspectionToken, long expectedFinalizedThrough)
        {
            Principal = principal ?? throw new ArgumentNullException(nameof(principal));
            if (!appId.IsValid || !accountId.IsValid || !UuidIdentity.IsValid(installationId) ||
                !priorStreamId.IsValid || !successorStreamId.IsValid || priorStreamId == successorStreamId ||
                !UuidIdentity.IsValid(successorStreamId.Value, false) || !UuidIdentity.IsValid(recoveryId, false) || expectedFinalizedThrough < 0)
                throw new ArgumentException("The recovery owner or identity is invalid.");
            if (principal.BackendNamespace.Value.Length > 128) throw new ArgumentOutOfRangeException(nameof(principal));
            if (reason != "local_database_recreated" && reason != "backup_restored" && reason != "stream_identity_conflict")
                throw new ArgumentOutOfRangeException(nameof(reason));
            if (inspectionToken == null || inspectionToken.Length < 16 || inspectionToken.Length > 4096 || !IsBase64Url(inspectionToken))
                throw new ArgumentOutOfRangeException(nameof(inspectionToken));
            AppId = appId; AccountId = accountId; InstallationId = installationId; PriorStreamId = priorStreamId;
            SuccessorStreamId = successorStreamId; RecoveryId = recoveryId; Reason = reason;
            InspectionToken = inspectionToken; ExpectedFinalizedThrough = expectedFinalizedThrough;
        }
        public AccountPrincipalDescriptor Principal { get; }
        public AppId AppId { get; }
        public PlatformUserId AccountId { get; }
        public Guid InstallationId { get; }
        public ClientStreamId PriorStreamId { get; }
        public ClientStreamId SuccessorStreamId { get; }
        public Guid RecoveryId { get; }
        public string Reason { get; }
        public string InspectionToken { get; }
        public long ExpectedFinalizedThrough { get; }

        private static bool IsBase64Url(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_')) return false;
            }
            return true;
        }
    }

    public sealed class CommandStreamRecoveryRecord
    {
        public CommandStreamRecoveryRecord(CommandStreamRecoveryRequest request, CommandStreamRecoveryPhase phase,
            long? remoteNextSequence, long? remotePriorFinalizedThrough)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            if (!Enum.IsDefined(typeof(CommandStreamRecoveryPhase), phase) ||
                (phase == CommandStreamRecoveryPhase.Prepared && (remoteNextSequence.HasValue || remotePriorFinalizedThrough.HasValue)) ||
                (phase >= CommandStreamRecoveryPhase.RemoteCommitted && (remoteNextSequence != 1 || remotePriorFinalizedThrough != request.ExpectedFinalizedThrough)) ||
                (phase < CommandStreamRecoveryPhase.RemoteCommitted && (remoteNextSequence.HasValue || remotePriorFinalizedThrough.HasValue)))
                throw new ArgumentException("Recovery phase evidence is inconsistent.");
            Phase = phase; RemoteNextSequence = remoteNextSequence; RemotePriorFinalizedThrough = remotePriorFinalizedThrough;
        }
        public CommandStreamRecoveryRequest Request { get; }
        public CommandStreamRecoveryPhase Phase { get; }
        public long? RemoteNextSequence { get; }
        public long? RemotePriorFinalizedThrough { get; }
    }

    /// <summary>Durable crash-recovery journal sharing the persistent principal directory lifetime.</summary>
    public interface ICommandStreamRecoveryJournal
    {
        Task<CommandStreamRecoveryRecord?> LoadAsync(CancellationToken cancellationToken);
        Task<CommandStreamRecoveryRecord> PrepareAsync(CommandStreamRecoveryRequest request, CancellationToken cancellationToken);
        Task<CommandStreamRecoveryRecord> RecordRemoteCommitAsync(Guid recoveryId, Guid returnedRecoveryId, ClientStreamId priorStreamId,
            ClientStreamId successorStreamId, long priorFinalizedThrough, long nextSequence, CancellationToken cancellationToken);
        Task<CommandStreamRecoveryRecord> MarkExpiredAsync(Guid recoveryId, CancellationToken cancellationToken);
        Task<CommandStreamRecoveryRecord> AdoptAsync(Guid recoveryId, CancellationToken cancellationToken);
        Task<CommandStreamRecoveryRecord> MarkBootstrapCompleteAsync(Guid recoveryId, CancellationToken cancellationToken);
    }
}
