using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlatform.Wire.Contracts.Teams
{

    public sealed class TeamsResponse
    {
        public TeamsResponse(Guid appId, Guid accountId, Guid playerId, long serverTimeMilliseconds, Guid? operationId, long revision, TeamSummary? team, TeamMembership? membership, IReadOnlyList<TeamSummary> teams, IReadOnlyList<TeamMember> members, IReadOnlyList<TeamMessage> messages, IReadOnlyList<TeamJoinRequest> requests, IReadOnlyList<TeamInvitation> invites, IReadOnlyList<TeamHelpRequest> helpRequests, string? nextCursor, string? resultCode)
        {
            if (appId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(appId));
            AppId = appId;
            if (accountId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(accountId));
            AccountId = accountId;
            if (playerId == Guid.Empty)
                throw new ArgumentException("A non-empty identity is required.", nameof(playerId));
            PlayerId = playerId;
            if (serverTimeMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(serverTimeMilliseconds));
            ServerTimeMilliseconds = serverTimeMilliseconds;
            OperationId = operationId;
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            Revision = revision;
            Team = team;
            Membership = membership;
            if (teams == null || teams.Count > 100 || teams.Any(item => item == null))
                throw new ArgumentException("A bounded non-null collection is required.", nameof(teams));
            Teams = Array.AsReadOnly(teams.ToArray());
            if (members == null || members.Count > 100 || members.Any(item => item == null))
                throw new ArgumentException("A bounded non-null collection is required.", nameof(members));
            Members = Array.AsReadOnly(members.ToArray());
            if (messages == null || messages.Count > 100 || messages.Any(item => item == null))
                throw new ArgumentException("A bounded non-null collection is required.", nameof(messages));
            Messages = Array.AsReadOnly(messages.ToArray());
            if (requests == null || requests.Count > 100 || requests.Any(item => item == null))
                throw new ArgumentException("A bounded non-null collection is required.", nameof(requests));
            Requests = Array.AsReadOnly(requests.ToArray());
            if (invites == null || invites.Count > 100 || invites.Any(item => item == null))
                throw new ArgumentException("A bounded non-null collection is required.", nameof(invites));
            Invites = Array.AsReadOnly(invites.ToArray());
            if (helpRequests == null || helpRequests.Count > 100 || helpRequests.Any(item => item == null))
                throw new ArgumentException("A bounded non-null collection is required.", nameof(helpRequests));
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
