using System;
using System.Linq;
using GamePlatform.Core;
namespace GamePlatform.Features.Teams
{
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
