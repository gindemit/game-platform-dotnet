using System;
using GamePlatform.Core;
namespace GamePlatform.Features.Leaderboards
{
    public sealed class LeaderboardsCountryCommand
    {
        public LeaderboardsCountryCommand(OperationId operationId, string boardId, string participantKind, string countryCode, Guid? teamId = null)
        {
            if (!operationId.IsValid || operationId.Value.ToString("D")[14] != '7')
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
        public OperationId OperationId { get; }
        public string BoardId { get; }
        public string ParticipantKind { get; }
        public string CountryCode { get; }
        public Guid? TeamId { get; }
    }
}
