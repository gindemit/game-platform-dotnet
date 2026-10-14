using System;
using System.Linq;

namespace GamePlatform.Wire.Contracts.Friends
{
    public sealed class FriendsCommandPayload
    {
        public FriendsCommandPayload(string? publicCode = null, Guid? targetPlayerId = null, Guid? requestId = null, bool? allowRequests = null)
        {
            if (publicCode != null)
                FriendsValidation.Code(publicCode);
            if (targetPlayerId.HasValue)
                FriendsValidation.Id(targetPlayerId.Value);
            if (requestId.HasValue)
                FriendsValidation.Operation(requestId.Value);
            PublicCode = publicCode;
            TargetPlayerId = targetPlayerId;
            RequestId = requestId;
            AllowRequests = allowRequests;
        }
        public string? PublicCode { get; }
        public Guid? TargetPlayerId { get; }
        public Guid? RequestId { get; }
        public bool? AllowRequests { get; }
    }
}
