using System;
namespace GamePlatform.Wire.Contracts.Teams
{
    /// <summary>Membership authority changed; shared social content must be queried again.</summary>
    public sealed class TeamsProjectionInvalidation : IProjectionChange
    {
        public TeamsProjectionInvalidation(Guid teamId, long revision)
        { if (teamId == Guid.Empty || revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision)); TeamId = teamId; Revision = revision; }
        public Guid TeamId { get; }
        public long Revision { get; }
        public string EntityType => "teams";
        public string Kind => "invalidation";
    }
}
