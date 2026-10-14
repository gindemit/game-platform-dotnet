using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Friends;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
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
