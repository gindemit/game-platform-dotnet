#nullable enable
using System;
using System.Linq;
using GamePlatform.Core;
namespace GamePlatform.Features.Teams
{
    public sealed class TeamsQuery
    {
        public TeamsQuery(string view, Guid? teamId = null, string? search = null, string? cursor = null, int pageSize = 30)
        {
            if (!new[] { "discover", "mine", "team", "members", "messages", "requests", "invites", "help" }.Contains(view)) throw new ArgumentOutOfRangeException(nameof(view));
            if (teamId == Guid.Empty || pageSize < 1 || pageSize > 50 || search?.Length > 64 || cursor?.Length > 128) throw new ArgumentOutOfRangeException(nameof(pageSize));
            View = view; TeamId = teamId; Search = search; Cursor = cursor; PageSize = pageSize;
        }
        public string View { get; }
        public Guid? TeamId { get; }
        public string? Search { get; }
        public string? Cursor { get; }
        public int PageSize { get; }
    }
    public sealed class TeamsCommand
    {
        public TeamsCommand(OperationId operationId, string action, Guid? teamId = null, long? expectedRevision = null, TeamsCommandPayload? payload = null)
        {
            if (!operationId.IsValid || string.IsNullOrWhiteSpace(action) || teamId == Guid.Empty || expectedRevision < 0) throw new ArgumentException("Invalid Teams command identity.");
            OperationId = operationId; Action = action; TeamId = teamId; ExpectedRevision = expectedRevision; Payload = payload ?? new TeamsCommandPayload();
        }
        public OperationId OperationId { get; }
        public string Action { get; }
        public Guid? TeamId { get; }
        public long? ExpectedRevision { get; }
        public TeamsCommandPayload Payload { get; }
    }
    public sealed class TeamsCommandPayload
    {
        public TeamsCommandPayload(string? name = null, string? description = null, string? badgeId = null, string? language = null, string? joinPolicy = null, int? requiredLevel = null, int? capacity = null, Guid? targetPlayerId = null, Guid? inviteId = null, Guid? messageId = null, Guid? requestId = null, string? role = null, string? text = null, string? reason = null, int? durationMinutes = null)
        {
            Name = name;
            Description = description;
            BadgeId = badgeId;
            Language = language;
            JoinPolicy = joinPolicy;
            RequiredLevel = requiredLevel;
            Capacity = capacity;
            TargetPlayerId = targetPlayerId;
            InviteId = inviteId;
            MessageId = messageId;
            RequestId = requestId;
            Role = role;
            Text = text;
            Reason = reason;
            DurationMinutes = durationMinutes;
        }
        public string? Name { get; }
        public string? Description { get; }
        public string? BadgeId { get; }
        public string? Language { get; }
        public string? JoinPolicy { get; }
        public int? RequiredLevel { get; }
        public int? Capacity { get; }
        public Guid? TargetPlayerId { get; }
        public Guid? InviteId { get; }
        public Guid? MessageId { get; }
        public Guid? RequestId { get; }
        public string? Role { get; }
        public string? Text { get; }
        public string? Reason { get; }
        public int? DurationMinutes { get; }
    }
}
