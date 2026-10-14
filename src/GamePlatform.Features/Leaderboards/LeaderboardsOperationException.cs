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
    public sealed class LeaderboardsOperationException : InvalidOperationException
    {
        public LeaderboardsOperationException(RemoteFailure failure, string resultCode)
            : base("The leaderboard operation could not be confirmed: " + SafeCode(resultCode))
        {
            Failure = failure;
            ResultCode = SafeCode(resultCode);
        }
        public RemoteFailure Failure { get; }
        public string ResultCode { get; }
        private static string SafeCode(string value) => !string.IsNullOrEmpty(value) && value.Length <= 96 &&
            value.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '.') ? value : "leaderboards_unavailable";
    }
}
