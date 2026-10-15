using System;
using System.Linq;
using GamePlatform.Backend.Contracts.Remote;

namespace GamePlatform.Features.Profiles
{
    public sealed class PublicProfileOperationException : InvalidOperationException
    {
        public PublicProfileOperationException(RemoteFailure failure, string resultCode)
            : base("The public profile read could not be confirmed: " + SafeCode(resultCode))
        {
            Failure = failure;
            ResultCode = SafeCode(resultCode);
        }
        public RemoteFailure Failure { get; }
        public string ResultCode { get; }
        private static string SafeCode(string value) => !string.IsNullOrEmpty(value) && value.Length <= 96 &&
            value.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '.') ? value : "profiles_public_unavailable";
    }
}
