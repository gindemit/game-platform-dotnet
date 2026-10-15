namespace GamePlatform.Features.Daily
{
    public interface IDailyStateCodec
    {
        byte[] EncodeStatus(DailyStatus value);
        DailyStatus DecodeStatus(byte[] bytes);
    }
}
