#nullable enable
using GamePlatform.Features.Leaderboards;
using GamePlatform.Serialization.MessagePack;
namespace GamePlatform.Transport.Http
{
    public sealed class LeaderboardsStateCodec : ILeaderboardsStateCodec
    {
        private readonly LeaderboardsMessagePackCodec codec = new LeaderboardsMessagePackCodec();
        public byte[] EncodeResponse(LeaderboardsResponse value) => codec.EncodeResponse(LeaderboardsModelMapper.ToWire(value));
        public LeaderboardsResponse DecodeResponse(byte[] bytes) => LeaderboardsModelMapper.ToDomain(codec.DecodeResponse(bytes));
        public byte[] EncodeCountry(LeaderboardsCountryCommand value) => codec.EncodeCountry(LeaderboardsModelMapper.ToWire(value));
        public LeaderboardsCountryCommand DecodeCountry(byte[] bytes) => LeaderboardsModelMapper.ToDomain(codec.DecodeCountry(bytes));
    }
}
