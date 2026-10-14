#nullable enable
using System.Linq;
using GamePlatform.Core;
using D = GamePlatform.Features.Leaderboards;
using W = GamePlatform.Wire.Contracts.Leaderboards;
namespace GamePlatform.Transport.Http
{
    internal static class LeaderboardsModelMapper
    {
        internal static GamePlatform.Wire.Contracts.Friends.FriendsRankingResponse ToFriendsWire(D.LeaderboardsResponse x) => new GamePlatform.Wire.Contracts.Friends.FriendsRankingResponse(x.BoardId, x.ParticipantKind, x.Scope, x.CountryCode, x.PeriodId, x.PeriodStart, x.PeriodEnd, x.ResetAt, x.SnapshotId, x.SnapshotCreatedAt, x.ExpiresAt, x.ScoreAuthority, x.Entries.Select(e => new GamePlatform.Wire.Contracts.Friends.FriendsRankingEntry(e.ParticipantId, e.Rank, e.Score, e.DisplayName, e.AvatarKey, e.BadgeId, e.CountryCode)).ToArray(), x.NextCursor, new GamePlatform.Wire.Contracts.Friends.FriendsRankingEntry(x.Self!.ParticipantId, x.Self.Rank, x.Self.Score, x.Self.DisplayName, x.Self.AvatarKey, x.Self.BadgeId, x.Self.CountryCode), x.SnapshotLimit, x.ServerTime, null, x.AppId, x.AccountId, x.PlayerId, x.GraphRevision, x.CohortSize);
        internal static D.LeaderboardsResponse ToDomain(GamePlatform.Wire.Contracts.Friends.FriendsRankingResponse x) => new D.LeaderboardsResponse(x.BoardId, x.ParticipantKind, x.Scope, x.CountryCode, x.PeriodId, x.PeriodStart, x.PeriodEnd, x.ResetAt, x.SnapshotId, x.SnapshotCreatedAt, x.ExpiresAt, x.ScoreAuthority, x.Entries.Select(e => new D.LeaderboardEntry(e.ParticipantId, e.Rank, e.Score, e.DisplayName, e.AvatarKey, e.BadgeId, e.CountryCode)).ToArray(), x.NextCursor, new D.LeaderboardEntry(x.Self!.ParticipantId, x.Self.Rank, x.Self.Score, x.Self.DisplayName, x.Self.AvatarKey, x.Self.BadgeId, x.Self.CountryCode), x.SnapshotLimit, x.ServerTime, null, x.AppId, x.AccountId, x.PlayerId, x.GraphRevision, x.CohortSize);
        internal static W.LeaderboardsQuery ToWire(D.LeaderboardsQuery x) => new W.LeaderboardsQuery(x.BoardId, x.Scope, x.CountryCode, x.Cursor, x.Limit);
        internal static W.LeaderboardsCountryCommand ToWire(D.LeaderboardsCountryCommand x) => new W.LeaderboardsCountryCommand(x.OperationId.Value, x.BoardId, x.ParticipantKind, x.CountryCode, x.TeamId);
        internal static D.LeaderboardsCountryCommand ToDomain(W.LeaderboardsCountryCommand x) => new D.LeaderboardsCountryCommand(new OperationId(x.OperationId), x.BoardId, x.ParticipantKind, x.CountryCode, x.TeamId);
        internal static W.LeaderboardsCountryResponse ToWire(D.LeaderboardsCountryResponse x) => new W.LeaderboardsCountryResponse(x.OperationId, x.BoardId, x.ParticipantKind, x.ParticipantId, x.CountryCode, x.EffectiveAt);
        internal static D.LeaderboardsCountryResponse ToDomain(W.LeaderboardsCountryResponse x) => new D.LeaderboardsCountryResponse(x.OperationId, x.BoardId, x.ParticipantKind, x.ParticipantId, x.CountryCode, x.EffectiveAt);
        private static W.LeaderboardEntry ToWire(D.LeaderboardEntry x) => new W.LeaderboardEntry(x.ParticipantId, x.Rank, x.Score, x.DisplayName, x.AvatarKey, x.BadgeId, x.CountryCode);
        private static D.LeaderboardEntry ToDomain(W.LeaderboardEntry x) => new D.LeaderboardEntry(x.ParticipantId, x.Rank, x.Score, x.DisplayName, x.AvatarKey, x.BadgeId, x.CountryCode);
        internal static W.LeaderboardsResponse ToWire(D.LeaderboardsResponse x) => new W.LeaderboardsResponse(x.BoardId, x.ParticipantKind, x.Scope, x.CountryCode,
            x.PeriodId, x.PeriodStart, x.PeriodEnd, x.ResetAt, x.SnapshotId, x.SnapshotCreatedAt, x.ExpiresAt, x.ScoreAuthority,
            x.Entries.Select(ToWire).ToArray(), x.NextCursor, x.Self == null ? null : ToWire(x.Self), x.SnapshotLimit, x.ServerTime,
            x.CountrySelection == null ? null : new W.LeaderboardsCountrySelection(x.CountrySelection.CurrentCountryCode, x.CountrySelection.PendingCountryCode, x.CountrySelection.PendingEffectiveAt));
        internal static D.LeaderboardsResponse ToDomain(W.LeaderboardsResponse x) => new D.LeaderboardsResponse(x.BoardId, x.ParticipantKind, x.Scope, x.CountryCode,
            x.PeriodId, x.PeriodStart, x.PeriodEnd, x.ResetAt, x.SnapshotId, x.SnapshotCreatedAt, x.ExpiresAt, x.ScoreAuthority,
            x.Entries.Select(ToDomain).ToArray(), x.NextCursor, x.Self == null ? null : ToDomain(x.Self), x.SnapshotLimit, x.ServerTime,
            x.CountrySelection == null ? null : new D.LeaderboardsCountrySelection(x.CountrySelection.CurrentCountryCode, x.CountrySelection.PendingCountryCode, x.CountrySelection.PendingEffectiveAt));
    }
}
