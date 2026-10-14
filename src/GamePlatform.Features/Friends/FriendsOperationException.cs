using System;
using System.Linq;
using GamePlatform.Backend.Contracts.Remote;
namespace GamePlatform.Features.Friends
{
    public sealed class FriendsOperationException : InvalidOperationException
    {
        public FriendsOperationException(RemoteFailure failure, string resultCode, Guid? retainedOperationId = null, long? retryAfterMilliseconds = null)
            : base("The Friends operation could not be confirmed: " + SafeCode(resultCode))
        {
            if (retainedOperationId == Guid.Empty || retryAfterMilliseconds < 0 || retryAfterMilliseconds > 253402300799999L)
                throw new ArgumentException("Invalid rejection metadata.");
            Failure = failure;
            ResultCode = SafeCode(resultCode);
            RetainedOperationId = IsBusinessRejection(failure.Kind, ResultCode) ? retainedOperationId : null;
            RetryAfterMilliseconds = retryAfterMilliseconds;
        }
        public RemoteFailure Failure { get; }
        public string ResultCode { get; }
        public Guid? RetainedOperationId { get; }
        public long? RetryAfterMilliseconds { get; }
        private static bool IsBusinessRejection(RemoteFailureKind kind, string code) => (kind == RemoteFailureKind.Validation || kind == RemoteFailureKind.Conflict || kind == RemoteFailureKind.RateLimited) && (code == "friends.target_unavailable" || code == "friends.already_friends" || code == "friends.outgoing_request_pending" || code == "friends.incoming_request_pending" || code == "friends.pair_cooldown" || code == "friends.friend_limit" || code == "friends.outgoing_limit" || code == "friends.send_cooldown" || code == "friends.request_unavailable" || code == "friends.request_expired" || code == "friends.not_friends");
        private static string SafeCode(string code) => !string.IsNullOrEmpty(code) && code.Length <= 96 && code.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '.') ? code : "friends.unavailable";
    }
}
