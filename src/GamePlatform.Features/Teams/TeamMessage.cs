using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlatform.Features.Teams
{

    public sealed class TeamMessage
    {
        public TeamMessage(Guid messageId, Guid playerId, string displayName, string text, string kind, long createdAtMilliseconds, bool deleted)
        {
            if (messageId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(messageId));
            MessageId = messageId;
            if (playerId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (displayName == null)
                throw new ArgumentNullException(nameof(displayName));
            DisplayName = displayName;
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            Text = text;
            if (kind == null)
                throw new ArgumentNullException(nameof(kind));
            Kind = kind;
            if (createdAtMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(createdAtMilliseconds));
            CreatedAtMilliseconds = createdAtMilliseconds;
            Deleted = deleted;
        }
        public Guid MessageId { get; }
        public Guid PlayerId { get; }
        public string DisplayName { get; }
        public string Text { get; }
        public string Kind { get; }
        public long CreatedAtMilliseconds { get; }
        public bool Deleted { get; }
    }

}
