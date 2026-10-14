#nullable enable
using GamePlatform.Features.Friends;
using GamePlatform.Serialization.MessagePack;
namespace GamePlatform.Transport.Http
{
    public sealed class FriendsStateCodec : IFriendsStateCodec
    {
        private readonly FriendsMessagePackCodec codec = new FriendsMessagePackCodec();
        public byte[] EncodeResponse(FriendsResponse value) => codec.EncodeResponse(FriendsModelMapper.ToW(value));
        public FriendsResponse DecodeResponse(byte[] bytes) => FriendsModelMapper.ToD(codec.DecodeResponse(bytes));
        public byte[] EncodeCommand(FriendsCommand value) => codec.EncodeCommand(FriendsModelMapper.ToW(value));
        public FriendsCommand DecodeCommand(byte[] bytes) => FriendsModelMapper.ToD(codec.DecodeCommand(bytes));
    }
}

