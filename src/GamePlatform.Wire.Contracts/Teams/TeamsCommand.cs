using System;
using System.Linq;
namespace GamePlatform.Wire.Contracts.Teams
{
    public sealed class TeamsCommand
    {
        public TeamsCommand(Guid operationId, string action, Guid? teamId = null, long? expectedRevision = null, TeamsCommandPayload? payload = null)
        {
            if (operationId == Guid.Empty || string.IsNullOrWhiteSpace(action) || teamId == Guid.Empty || expectedRevision < 0)
                throw new ArgumentException("Invalid Teams command identity.");
            OperationId = operationId;
            Action = action;
            TeamId = teamId;
            ExpectedRevision = expectedRevision;
            Payload = payload ?? new TeamsCommandPayload();
        }
        public Guid OperationId { get; }
        public string Action { get; }
        public Guid? TeamId { get; }
        public long? ExpectedRevision { get; }
        public TeamsCommandPayload Payload { get; }
    }
}
