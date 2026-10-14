using System;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public sealed class ProvisioningSnapshot
    {
        public ProvisioningSnapshot(PlatformUserId accountId, AppId appId, string membershipStatus, ClientStreamId streamId, long nextSequence, long serverTime)
        {
            if (!accountId.IsValid || !appId.IsValid || !streamId.IsValid)
                throw new ArgumentException("Valid provisioned identities are required.");
            if (string.IsNullOrWhiteSpace(membershipStatus) || membershipStatus.Length > 64)
                throw new ArgumentOutOfRangeException(nameof(membershipStatus));
            if (nextSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(nextSequence));
            if (serverTime < 0 || serverTime > 253_402_300_799_999L)
                throw new ArgumentOutOfRangeException(nameof(serverTime));
            AccountId = accountId;
            AppId = appId;
            MembershipStatus = membershipStatus;
            StreamId = streamId;
            NextSequence = nextSequence;
            ServerTime = serverTime;
        }
        public PlatformUserId AccountId { get; }
        public AppId AppId { get; }
        public string MembershipStatus { get; }
        public ClientStreamId StreamId { get; }
        public long NextSequence { get; }
        public long ServerTime { get; }
    }
}
