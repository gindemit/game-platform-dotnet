namespace GamePlatform.Features.Profiles
{
    public interface IPublicProfileStateCodec
    {
        byte[] EncodeResponse(PublicProfileResponse value);
        PublicProfileResponse DecodeResponse(byte[] bytes);
    }
}
