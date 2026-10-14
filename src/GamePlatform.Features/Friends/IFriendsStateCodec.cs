namespace GamePlatform.Features.Friends
{
    public interface IFriendsStateCodec
    {
        byte[] EncodeResponse(FriendsResponse value);
        FriendsResponse DecodeResponse(byte[] bytes);
        byte[] EncodeCommand(FriendsCommand value);
        FriendsCommand DecodeCommand(byte[] bytes);
    }
}
