namespace GamePlatform.Features.Daily
{
    public interface IDailyClaimCommandCodec
    {
        byte[] EncodeClaimCommand(string utcDate);
    }
}
