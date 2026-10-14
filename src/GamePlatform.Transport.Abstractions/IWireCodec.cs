namespace GamePlatform.Transport.Abstractions
{
    public interface IWireCodec
    {
        byte[] Encode<T>(T value); T Decode<T>(byte[] payload);
    }
}
