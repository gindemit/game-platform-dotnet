#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlatform.Wire.Contracts.Teams
{
    public sealed class TeamSummary
    {
        public TeamSummary(Guid teamId, string name, string description, string badgeId, string language, string joinPolicy, int requiredLevel, int capacity, int memberCount, long revision, long createdAtMilliseconds)
        {
            if (teamId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(teamId));
            TeamId = teamId;
            if (name == null) throw new ArgumentNullException(nameof(name));
            Name = name;
            if (description == null) throw new ArgumentNullException(nameof(description));
            Description = description;
            if (badgeId == null) throw new ArgumentNullException(nameof(badgeId));
            BadgeId = badgeId;
            if (language == null) throw new ArgumentNullException(nameof(language));
            Language = language;
            if (joinPolicy == null) throw new ArgumentNullException(nameof(joinPolicy));
            JoinPolicy = joinPolicy;
            if (requiredLevel < 0) throw new ArgumentOutOfRangeException(nameof(requiredLevel));
            RequiredLevel = requiredLevel;
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            if (memberCount < 0) throw new ArgumentOutOfRangeException(nameof(memberCount));
            MemberCount = memberCount;
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            Revision = revision;
            if (createdAtMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(createdAtMilliseconds));
            CreatedAtMilliseconds = createdAtMilliseconds;
        }
        public Guid TeamId { get; }
        public string Name { get; }
        public string Description { get; }
        public string BadgeId { get; }
        public string Language { get; }
        public string JoinPolicy { get; }
        public int RequiredLevel { get; }
        public int Capacity { get; }
        public int MemberCount { get; }
        public long Revision { get; }
        public long CreatedAtMilliseconds { get; }
    }

    public sealed class TeamMembership
    {
        public TeamMembership(Guid teamId, Guid playerId, string role, long joinedAtMilliseconds, long helpCount)
        {
            if (teamId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(teamId));
            TeamId = teamId;
            if (playerId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (role == null) throw new ArgumentNullException(nameof(role));
            Role = role;
            if (joinedAtMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(joinedAtMilliseconds));
            JoinedAtMilliseconds = joinedAtMilliseconds;
            if (helpCount < 0) throw new ArgumentOutOfRangeException(nameof(helpCount));
            HelpCount = helpCount;
        }
        public Guid TeamId { get; }
        public Guid PlayerId { get; }
        public string Role { get; }
        public long JoinedAtMilliseconds { get; }
        public long HelpCount { get; }
    }

    public sealed class TeamMember
    {
        public TeamMember(Guid teamId, Guid playerId, string role, long joinedAtMilliseconds, long helpCount, string displayName, string? avatarKey, long mutedUntilMilliseconds)
        {
            if (teamId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(teamId));
            TeamId = teamId;
            if (playerId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (role == null) throw new ArgumentNullException(nameof(role));
            Role = role;
            if (joinedAtMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(joinedAtMilliseconds));
            JoinedAtMilliseconds = joinedAtMilliseconds;
            if (helpCount < 0) throw new ArgumentOutOfRangeException(nameof(helpCount));
            HelpCount = helpCount;
            if (displayName == null) throw new ArgumentNullException(nameof(displayName));
            DisplayName = displayName;
            AvatarKey = avatarKey;
            if (mutedUntilMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(mutedUntilMilliseconds));
            MutedUntilMilliseconds = mutedUntilMilliseconds;
        }
        public Guid TeamId { get; }
        public Guid PlayerId { get; }
        public string Role { get; }
        public long JoinedAtMilliseconds { get; }
        public long HelpCount { get; }
        public string DisplayName { get; }
        public string? AvatarKey { get; }
        public long MutedUntilMilliseconds { get; }
    }

    public sealed class TeamMessage
    {
        public TeamMessage(Guid messageId, Guid playerId, string displayName, string text, string kind, long createdAtMilliseconds, bool deleted)
        {
            if (messageId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(messageId));
            MessageId = messageId;
            if (playerId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (displayName == null) throw new ArgumentNullException(nameof(displayName));
            DisplayName = displayName;
            if (text == null) throw new ArgumentNullException(nameof(text));
            Text = text;
            if (kind == null) throw new ArgumentNullException(nameof(kind));
            Kind = kind;
            if (createdAtMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(createdAtMilliseconds));
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

    public sealed class TeamJoinRequest
    {
        public TeamJoinRequest(Guid requestId, Guid teamId, Guid playerId, string displayName, string status, long createdAtMilliseconds)
        {
            if (requestId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(requestId));
            RequestId = requestId;
            if (teamId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(teamId));
            TeamId = teamId;
            if (playerId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (displayName == null) throw new ArgumentNullException(nameof(displayName));
            DisplayName = displayName;
            if (status == null) throw new ArgumentNullException(nameof(status));
            Status = status;
            if (createdAtMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(createdAtMilliseconds));
            CreatedAtMilliseconds = createdAtMilliseconds;
        }
        public Guid RequestId { get; }
        public Guid TeamId { get; }
        public Guid PlayerId { get; }
        public string DisplayName { get; }
        public string Status { get; }
        public long CreatedAtMilliseconds { get; }
    }

    public sealed class TeamInvitation
    {
        public TeamInvitation(Guid inviteId, Guid teamId, Guid playerId, Guid inviterPlayerId, string status, long createdAtMilliseconds, long expiresAtMilliseconds)
        {
            if (inviteId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(inviteId));
            InviteId = inviteId;
            if (teamId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(teamId));
            TeamId = teamId;
            if (playerId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (inviterPlayerId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(inviterPlayerId));
            InviterPlayerId = inviterPlayerId;
            if (status == null) throw new ArgumentNullException(nameof(status));
            Status = status;
            if (createdAtMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(createdAtMilliseconds));
            CreatedAtMilliseconds = createdAtMilliseconds;
            if (expiresAtMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(expiresAtMilliseconds));
            ExpiresAtMilliseconds = expiresAtMilliseconds;
        }
        public Guid InviteId { get; }
        public Guid TeamId { get; }
        public Guid PlayerId { get; }
        public Guid InviterPlayerId { get; }
        public string Status { get; }
        public long CreatedAtMilliseconds { get; }
        public long ExpiresAtMilliseconds { get; }
    }

    public sealed class TeamHelpRequest
    {
        public TeamHelpRequest(Guid requestId, Guid teamId, Guid playerId, string displayName, int currentCount, int requiredCount, long createdAtMilliseconds, long expiresAtMilliseconds, bool contributedByMe, string status)
        {
            if (requestId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(requestId));
            RequestId = requestId;
            if (teamId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(teamId));
            TeamId = teamId;
            if (playerId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (displayName == null) throw new ArgumentNullException(nameof(displayName));
            DisplayName = displayName;
            if (currentCount < 0) throw new ArgumentOutOfRangeException(nameof(currentCount));
            CurrentCount = currentCount;
            if (requiredCount < 0) throw new ArgumentOutOfRangeException(nameof(requiredCount));
            RequiredCount = requiredCount;
            if (createdAtMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(createdAtMilliseconds));
            CreatedAtMilliseconds = createdAtMilliseconds;
            if (expiresAtMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(expiresAtMilliseconds));
            ExpiresAtMilliseconds = expiresAtMilliseconds;
            ContributedByMe = contributedByMe;
            if (status == null) throw new ArgumentNullException(nameof(status));
            Status = status;
        }
        public Guid RequestId { get; }
        public Guid TeamId { get; }
        public Guid PlayerId { get; }
        public string DisplayName { get; }
        public int CurrentCount { get; }
        public int RequiredCount { get; }
        public long CreatedAtMilliseconds { get; }
        public long ExpiresAtMilliseconds { get; }
        public bool ContributedByMe { get; }
        public string Status { get; }
    }

    public sealed class TeamsResponse
    {
        public TeamsResponse(Guid appId, Guid accountId, Guid playerId, long serverTimeMilliseconds, Guid? operationId, long revision, TeamSummary? team, TeamMembership? membership, IReadOnlyList<TeamSummary> teams, IReadOnlyList<TeamMember> members, IReadOnlyList<TeamMessage> messages, IReadOnlyList<TeamJoinRequest> requests, IReadOnlyList<TeamInvitation> invites, IReadOnlyList<TeamHelpRequest> helpRequests, string? nextCursor, string? resultCode)
        {
            if (appId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(appId));
            AppId = appId;
            if (accountId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(accountId));
            AccountId = accountId;
            if (playerId == Guid.Empty) throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (serverTimeMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(serverTimeMilliseconds));
            ServerTimeMilliseconds = serverTimeMilliseconds;
            OperationId = operationId;
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            Revision = revision;
            Team = team;
            Membership = membership;
            if (teams == null || teams.Count > 100 || teams.Any(item => item == null)) throw new ArgumentException("A bounded non-null collection is required.", nameof(teams));
            Teams = Array.AsReadOnly(teams.ToArray());
            if (members == null || members.Count > 100 || members.Any(item => item == null)) throw new ArgumentException("A bounded non-null collection is required.", nameof(members));
            Members = Array.AsReadOnly(members.ToArray());
            if (messages == null || messages.Count > 100 || messages.Any(item => item == null)) throw new ArgumentException("A bounded non-null collection is required.", nameof(messages));
            Messages = Array.AsReadOnly(messages.ToArray());
            if (requests == null || requests.Count > 100 || requests.Any(item => item == null)) throw new ArgumentException("A bounded non-null collection is required.", nameof(requests));
            Requests = Array.AsReadOnly(requests.ToArray());
            if (invites == null || invites.Count > 100 || invites.Any(item => item == null)) throw new ArgumentException("A bounded non-null collection is required.", nameof(invites));
            Invites = Array.AsReadOnly(invites.ToArray());
            if (helpRequests == null || helpRequests.Count > 100 || helpRequests.Any(item => item == null)) throw new ArgumentException("A bounded non-null collection is required.", nameof(helpRequests));
            HelpRequests = Array.AsReadOnly(helpRequests.ToArray());
            NextCursor = nextCursor;
            ResultCode = resultCode;
        }
        public Guid AppId { get; }
        public Guid AccountId { get; }
        public Guid PlayerId { get; }
        public long ServerTimeMilliseconds { get; }
        public Guid? OperationId { get; }
        public long Revision { get; }
        public TeamSummary? Team { get; }
        public TeamMembership? Membership { get; }
        public IReadOnlyList<TeamSummary> Teams { get; }
        public IReadOnlyList<TeamMember> Members { get; }
        public IReadOnlyList<TeamMessage> Messages { get; }
        public IReadOnlyList<TeamJoinRequest> Requests { get; }
        public IReadOnlyList<TeamInvitation> Invites { get; }
        public IReadOnlyList<TeamHelpRequest> HelpRequests { get; }
        public string? NextCursor { get; }
        public string? ResultCode { get; }
    }

}
