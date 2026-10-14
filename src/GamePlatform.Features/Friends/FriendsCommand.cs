using System;
using System.Linq;
using GamePlatform.Core;
namespace GamePlatform.Features.Friends
{
    public sealed class FriendsCommand
    {
        public FriendsCommand(OperationId operationId, string action, FriendsCommandPayload? payload = null)
        {
            FriendsValidation.Operation(operationId.Value);
            if (!FriendsValidation.Actions.Contains(action))
                throw new ArgumentException("Invalid friends action.");
            payload = payload ?? new FriendsCommandPayload();
            var p = payload;
            int fields = (p.PublicCode != null ? 1 : 0) + (p.TargetPlayerId.HasValue ? 1 : 0) + (p.RequestId.HasValue ? 1 : 0) + (p.AllowRequests.HasValue ? 1 : 0);
            if (fields != 1 || action == "send" && p.PublicCode == null && !p.TargetPlayerId.HasValue || new[] { "accept", "decline", "cancel" }.Contains(action) && !p.RequestId.HasValue || new[] { "remove", "block", "unblock" }.Contains(action) && !p.TargetPlayerId.HasValue || action == "set_privacy" && !p.AllowRequests.HasValue)
                throw new ArgumentException("Invalid friends action payload.");
            OperationId = operationId;
            Action = action;
            Payload = payload;
        }
        public OperationId OperationId { get; }
        public string Action { get; }
        public FriendsCommandPayload Payload { get; }
    }
}

