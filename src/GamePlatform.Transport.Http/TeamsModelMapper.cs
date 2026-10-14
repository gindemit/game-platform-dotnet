#nullable enable
using System;
using System.Linq;
using D = GamePlatform.Features.Teams;
using W = GamePlatform.Wire.Contracts.Teams;

namespace GamePlatform.Transport.Http
{
    internal static class TeamsModelMapper
    {
        internal static D.TeamSummary ToD(W.TeamSummary value) => new D.TeamSummary(value.TeamId, value.Name, value.Description, value.BadgeId, value.Language, value.JoinPolicy, value.RequiredLevel, value.Capacity, value.MemberCount, value.Revision, value.CreatedAtMilliseconds);
        internal static D.TeamMembership ToD(W.TeamMembership value) => new D.TeamMembership(value.TeamId, value.PlayerId, value.Role, value.JoinedAtMilliseconds, value.HelpCount);
        internal static D.TeamMember ToD(W.TeamMember value) => new D.TeamMember(value.TeamId, value.PlayerId, value.Role, value.JoinedAtMilliseconds, value.HelpCount, value.DisplayName, value.AvatarKey, value.MutedUntilMilliseconds);
        internal static D.TeamMessage ToD(W.TeamMessage value) => new D.TeamMessage(value.MessageId, value.PlayerId, value.DisplayName, value.Text, value.Kind, value.CreatedAtMilliseconds, value.Deleted);
        internal static D.TeamJoinRequest ToD(W.TeamJoinRequest value) => new D.TeamJoinRequest(value.RequestId, value.TeamId, value.PlayerId, value.DisplayName, value.Status, value.CreatedAtMilliseconds);
        internal static D.TeamInvitation ToD(W.TeamInvitation value) => new D.TeamInvitation(value.InviteId, value.TeamId, value.PlayerId, value.InviterPlayerId, value.Status, value.CreatedAtMilliseconds, value.ExpiresAtMilliseconds);
        internal static D.TeamHelpRequest ToD(W.TeamHelpRequest value) => new D.TeamHelpRequest(value.RequestId, value.TeamId, value.PlayerId, value.DisplayName, value.CurrentCount, value.RequiredCount, value.CreatedAtMilliseconds, value.ExpiresAtMilliseconds, value.ContributedByMe, value.Status);
        internal static D.TeamsResponse ToD(W.TeamsResponse value) => new D.TeamsResponse(value.AppId, value.AccountId, value.PlayerId, value.ServerTimeMilliseconds, value.OperationId, value.Revision, value.Team == null ? null : ToD(value.Team), value.Membership == null ? null : ToD(value.Membership), value.Teams.Select(ToD).ToArray(), value.Members.Select(ToD).ToArray(), value.Messages.Select(ToD).ToArray(), value.Requests.Select(ToD).ToArray(), value.Invites.Select(ToD).ToArray(), value.HelpRequests.Select(ToD).ToArray(), value.NextCursor, value.ResultCode);
        internal static W.TeamSummary ToW(D.TeamSummary value) => new W.TeamSummary(value.TeamId, value.Name, value.Description, value.BadgeId, value.Language, value.JoinPolicy, value.RequiredLevel, value.Capacity, value.MemberCount, value.Revision, value.CreatedAtMilliseconds);
        internal static W.TeamMembership ToW(D.TeamMembership value) => new W.TeamMembership(value.TeamId, value.PlayerId, value.Role, value.JoinedAtMilliseconds, value.HelpCount);
        internal static W.TeamMember ToW(D.TeamMember value) => new W.TeamMember(value.TeamId, value.PlayerId, value.Role, value.JoinedAtMilliseconds, value.HelpCount, value.DisplayName, value.AvatarKey, value.MutedUntilMilliseconds);
        internal static W.TeamMessage ToW(D.TeamMessage value) => new W.TeamMessage(value.MessageId, value.PlayerId, value.DisplayName, value.Text, value.Kind, value.CreatedAtMilliseconds, value.Deleted);
        internal static W.TeamJoinRequest ToW(D.TeamJoinRequest value) => new W.TeamJoinRequest(value.RequestId, value.TeamId, value.PlayerId, value.DisplayName, value.Status, value.CreatedAtMilliseconds);
        internal static W.TeamInvitation ToW(D.TeamInvitation value) => new W.TeamInvitation(value.InviteId, value.TeamId, value.PlayerId, value.InviterPlayerId, value.Status, value.CreatedAtMilliseconds, value.ExpiresAtMilliseconds);
        internal static W.TeamHelpRequest ToW(D.TeamHelpRequest value) => new W.TeamHelpRequest(value.RequestId, value.TeamId, value.PlayerId, value.DisplayName, value.CurrentCount, value.RequiredCount, value.CreatedAtMilliseconds, value.ExpiresAtMilliseconds, value.ContributedByMe, value.Status);
        internal static W.TeamsResponse ToW(D.TeamsResponse value) => new W.TeamsResponse(value.AppId, value.AccountId, value.PlayerId, value.ServerTimeMilliseconds, value.OperationId, value.Revision, value.Team == null ? null : ToW(value.Team), value.Membership == null ? null : ToW(value.Membership), value.Teams.Select(ToW).ToArray(), value.Members.Select(ToW).ToArray(), value.Messages.Select(ToW).ToArray(), value.Requests.Select(ToW).ToArray(), value.Invites.Select(ToW).ToArray(), value.HelpRequests.Select(ToW).ToArray(), value.NextCursor, value.ResultCode);
    }
}
