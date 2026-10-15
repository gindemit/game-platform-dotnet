using GamePlatform.Features.Profiles;
using GamePlatform.Serialization.MessagePack;

namespace GamePlatform.Transport.Http
{
    public sealed class PublicProfileStateCodec : IPublicProfileStateCodec
    {
        private readonly PublicProfileMessagePackCodec codec = new PublicProfileMessagePackCodec();
        public byte[] EncodeResponse(PublicProfileResponse value) => codec.EncodeResponse(PublicProfileModelMapper.ToWire(value));
        public PublicProfileResponse DecodeResponse(byte[] bytes) => PublicProfileModelMapper.ToDomain(codec.DecodeResponse(bytes));
    }
}
