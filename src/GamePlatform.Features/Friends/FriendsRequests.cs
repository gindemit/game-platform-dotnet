#nullable enable
using System;
using System.Linq;
using GamePlatform.Core;
namespace GamePlatform.Features.Friends
{
    public sealed class FriendsQuery
    {
        public FriendsQuery(string view, string? publicCode = null, string? cursor = null, int pageSize = 30)
        {
            FriendsValidation.View(view);
            FriendsValidation.Cursor(cursor);
            if (pageSize < 1 || pageSize > 50 || (view == "lookup") != (publicCode != null) || (view == "lookup" || view == "mine") && cursor != null)
                throw new ArgumentException("Invalid friends query.");
            if (publicCode != null)
                FriendsValidation.Code(publicCode);
            View = view;
            PublicCode = publicCode;
            Cursor = cursor;
            PageSize = pageSize;
        }
        public string View
        {
            get;
        }
        public string? PublicCode
        {
            get;
        }
        public string? Cursor
        {
            get;
        }
        public int PageSize
        {
            get;
        }
    }
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
        public OperationId OperationId
        {
            get;
        }
        public string Action
        {
            get;
        }
        public FriendsCommandPayload Payload
        {
            get;
        }
    }
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
        public string? PublicCode
        {
            get;
        }
        public Guid? TargetPlayerId
        {
            get;
        }
        public Guid? RequestId
        {
            get;
        }
        public bool? AllowRequests
        {
            get;
        }
    }
}

