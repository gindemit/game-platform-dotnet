using System;

namespace GamePlatform.Features.Leaderboards
{

    public sealed class LeaderboardsCountryResponse
    {
        public LeaderboardsCountryResponse(Guid operationId, string boardId, string participantKind, Guid participantId, string countryCode, long effectiveAt)
        {
            if (operationId == Guid.Empty || participantId == Guid.Empty)
                throw new ArgumentException("Valid identities are required.");
            LeaderboardsValidation.Board(boardId);
            LeaderboardsValidation.Kind(participantKind);
            LeaderboardsValidation.Country(countryCode, false);
            LeaderboardsValidation.Time(effectiveAt);
            OperationId = operationId;
            BoardId = boardId;
            ParticipantKind = participantKind;
            ParticipantId = participantId;
            CountryCode = countryCode;
            EffectiveAt = effectiveAt;
        }
        public Guid OperationId { get; }
        public string BoardId { get; }
        public string ParticipantKind { get; }
        public Guid ParticipantId { get; }
        public string CountryCode { get; }
        public long EffectiveAt { get; }
    }
}
