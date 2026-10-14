#nullable enable
using System;
using GamePlatform.Features.Leaderboards;
using GamePlatform.Serialization.MessagePack;
namespace GamePlatform.Transport.Http
{
    public sealed class LeaderboardsStateCodec : ILeaderboardsStateCodec
    {
        private readonly LeaderboardsMessagePackCodec codec = new LeaderboardsMessagePackCodec();
        private readonly FriendsMessagePackCodec friends = new FriendsMessagePackCodec();
        public byte[] EncodeResponse(LeaderboardsResponse value)
        {
            if (value.Scope != "friends")
                return codec.EncodeResponse(LeaderboardsModelMapper.ToWire(value));
            var payload = friends.EncodeRankingResponse(LeaderboardsModelMapper.ToFriendsWire(value));
            var tagged = new byte[payload.Length + 1];
            tagged[0] = 0xc1;
            Array.Copy(payload, 0, tagged, 1, payload.Length);
            return tagged;
        }
        public LeaderboardsResponse DecodeResponse(byte[] bytes)
        {
            // A reserved MessagePack byte tags only local SQL rows; HTTP always uses the canonical untagged payload.
            if (bytes == null || bytes.Length == 0 || bytes.Length > 262144)
                throw new ArgumentException("Invalid stored standings.");
            if (bytes[0] != 0xc1)
                return LeaderboardsModelMapper.ToDomain(codec.DecodeResponse(bytes));
            var payload = new byte[bytes.Length - 1];
            Array.Copy(bytes, 1, payload, 0, payload.Length);
            return LeaderboardsModelMapper.ToDomain(friends.DecodeRankingResponse(payload));
        }
        public byte[] EncodeCountry(LeaderboardsCountryCommand value) => codec.EncodeCountry(LeaderboardsModelMapper.ToWire(value));
        public LeaderboardsCountryCommand DecodeCountry(byte[] bytes) => LeaderboardsModelMapper.ToDomain(codec.DecodeCountry(bytes));
    }
}
