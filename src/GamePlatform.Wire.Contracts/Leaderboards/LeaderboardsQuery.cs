using System;
namespace GamePlatform.Wire.Contracts.Leaderboards
{
    public sealed class LeaderboardsQuery
    {
        public LeaderboardsQuery(string boardId, string scope = "global", string? countryCode = null, string? cursor = null, int limit = 30)
        {
            LeaderboardsValidation.Board(boardId);
            LeaderboardsValidation.Scope(scope, countryCode);
            LeaderboardsValidation.Cursor(cursor);
            if (limit < 1 || limit > 50)
                throw new ArgumentOutOfRangeException(nameof(limit));
            BoardId = boardId;
            Scope = scope;
            CountryCode = countryCode;
            Cursor = cursor;
            Limit = limit;
        }
        public string BoardId { get; }
        public string Scope { get; }
        public string? CountryCode { get; }
        public string? Cursor { get; }
        public int Limit { get; }
    }
}
