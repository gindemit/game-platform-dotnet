#nullable enable
using GamePlatform.Core;
using GamePlatform.Features.Teams;
using GamePlatform.Serialization.MessagePack;
using W = GamePlatform.Wire.Contracts.Teams;
namespace GamePlatform.Transport.Http
{
    /// <summary>Offline-safe Teams durable codec; construction owns no transport or authentication.</summary>
    public sealed class TeamsStateCodec : ITeamsStateCodec
    {
        private readonly TeamsMessagePackCodec codec = new TeamsMessagePackCodec();
        public byte[] EncodeResponse(TeamsResponse response) => codec.EncodeResponse(TeamsModelMapper.ToW(response));
        public TeamsResponse DecodeResponse(byte[] bytes) => TeamsModelMapper.ToD(codec.DecodeResponse(bytes));
        public byte[] EncodeCommand(TeamsCommand command)
        {
            var p = command.Payload;
            return codec.EncodeCommand(new W.TeamsCommand(command.OperationId.Value, command.Action, command.TeamId, command.ExpectedRevision,
                new W.TeamsCommandPayload(p.Name, p.Description, p.BadgeId, p.Language, p.JoinPolicy, p.RequiredLevel, p.Capacity, p.TargetPlayerId,
                    p.InviteId, p.MessageId, p.RequestId, p.Role, p.Text, p.Reason, p.DurationMinutes)));
        }
        public TeamsCommand DecodeCommand(byte[] bytes)
        {
            var value = codec.DecodeCommand(bytes); var p = value.Payload;
            return new TeamsCommand(new OperationId(value.OperationId), value.Action, value.TeamId, value.ExpectedRevision,
                new TeamsCommandPayload(p.Name, p.Description, p.BadgeId, p.Language, p.JoinPolicy, p.RequiredLevel, p.Capacity, p.TargetPlayerId,
                    p.InviteId, p.MessageId, p.RequestId, p.Role, p.Text, p.Reason, p.DurationMinutes));
        }
    }
}
