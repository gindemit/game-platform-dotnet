using System;
using GamePlatform.Core;
namespace GamePlatform.Features.Teams
{
    public sealed class TeamsCommand
    {
        public TeamsCommand(OperationId operationId, string action, Guid? teamId = null, long? expectedRevision = null, TeamsCommandPayload? payload = null)
        {
            if (!operationId.IsValid || string.IsNullOrWhiteSpace(action) || teamId == Guid.Empty || expectedRevision < 0)
                throw new ArgumentException("Invalid Teams command identity.");
            OperationId = operationId;
            Action = action;
            TeamId = teamId;
            ExpectedRevision = expectedRevision;
            Payload = payload ?? new TeamsCommandPayload();
        }
        public OperationId OperationId { get; }
        public string Action { get; }
        public Guid? TeamId { get; }
        public long? ExpectedRevision { get; }
        public TeamsCommandPayload Payload { get; }
    }
}
