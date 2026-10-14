using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
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
