using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions.Accounts;

namespace GamePlatform.Storage.Abstractions.Recovery
{

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
            if (principal.BackendNamespace.Value.Length > 128)
                throw new ArgumentOutOfRangeException(nameof(principal));
            if (reason != "local_database_recreated" && reason != "backup_restored" && reason != "stream_identity_conflict")
                throw new ArgumentOutOfRangeException(nameof(reason));
            if (inspectionToken == null || inspectionToken.Length < 16 || inspectionToken.Length > 4096 || !IsBase64Url(inspectionToken))
                throw new ArgumentOutOfRangeException(nameof(inspectionToken));
            AppId = appId;
            AccountId = accountId;
            InstallationId = installationId;
            PriorStreamId = priorStreamId;
            SuccessorStreamId = successorStreamId;
            RecoveryId = recoveryId;
            Reason = reason;
            InspectionToken = inspectionToken;
            ExpectedFinalizedThrough = expectedFinalizedThrough;
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
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                    return false;
            }
            return true;
        }
    }
}
