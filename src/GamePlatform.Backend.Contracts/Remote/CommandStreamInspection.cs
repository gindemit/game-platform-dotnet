using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

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

        public ClientStreamId StreamId { get; }
        public Guid InstallationId { get; }
        public long FinalizedThrough { get; }
        public long? NextSequence { get; }
        public CommandStreamState State { get; }
        public string InspectionToken { get; }
        public long ExpiresAt { get; }
        public long ServerTime { get; }

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
}
