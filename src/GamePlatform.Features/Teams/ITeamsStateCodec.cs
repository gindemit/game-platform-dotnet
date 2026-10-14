namespace GamePlatform.Features.Teams
{

    public interface ITeamsStateCodec
    {
        byte[] EncodeResponse(TeamsResponse response);
        TeamsResponse DecodeResponse(byte[] bytes);
        byte[] EncodeCommand(TeamsCommand command);
        TeamsCommand DecodeCommand(byte[] bytes);
    }
}
