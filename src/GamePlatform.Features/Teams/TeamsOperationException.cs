using System;
using System.Linq;
using GamePlatform.Backend.Contracts.Remote;

namespace GamePlatform.Features.Teams
{

    public sealed class TeamsOperationException : InvalidOperationException
    {
        public TeamsOperationException(RemoteFailure failure, string resultCode)
            : base("The Teams operation could not be confirmed: " + SafeCode(resultCode))
        {
            Failure = failure;
            ResultCode = SafeCode(resultCode);
        }
        public RemoteFailure Failure { get; }
        public string ResultCode { get; }
        private static string SafeCode(string value) => !string.IsNullOrEmpty(value) && value.Length <= 96 &&
            value.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '.') ? value : "teams_unavailable";
    }
}
