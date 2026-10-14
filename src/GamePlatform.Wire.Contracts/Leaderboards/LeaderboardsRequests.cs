#nullable enable
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
        public string BoardId
        {
            get;
        }
        public string Scope
        {
            get;
        }
        public string? CountryCode
        {
            get;
        }
        public string? Cursor
        {
            get;
        }
        public int Limit
        {
            get;
        }
    }
    public sealed class LeaderboardsCountryCommand
    {
        public LeaderboardsCountryCommand(Guid operationId, string boardId, string participantKind, string countryCode, Guid? teamId = null)
        {
            if (operationId == Guid.Empty || operationId.ToString("D")[14] != '7')
                throw new ArgumentException("UUIDv7 operation identity required.", nameof(operationId));
            LeaderboardsValidation.Board(boardId);
            LeaderboardsValidation.Kind(participantKind);
            LeaderboardsValidation.Country(countryCode, false);
            if ((participantKind == "team") != teamId.HasValue || teamId == Guid.Empty)
                throw new ArgumentException("Team country requires exactly one team identity.");
            OperationId = operationId;
            BoardId = boardId;
            ParticipantKind = participantKind;
            CountryCode = countryCode;
            TeamId = teamId;
        }
        public Guid OperationId
        {
            get;
        }
        public string BoardId
        {
            get;
        }
        public string ParticipantKind
        {
            get;
        }
        public string CountryCode
        {
            get;
        }
        public Guid? TeamId
        {
            get;
        }
    }
}
