namespace GamePlatform.Features.Leaderboards
{
    public interface ILeaderboardsStateCodec
    {
        byte[] EncodeResponse(LeaderboardsResponse value);
        LeaderboardsResponse DecodeResponse(byte[] bytes);
        byte[] EncodeCountry(LeaderboardsCountryCommand value);
        LeaderboardsCountryCommand DecodeCountry(byte[] bytes);
    }
}
