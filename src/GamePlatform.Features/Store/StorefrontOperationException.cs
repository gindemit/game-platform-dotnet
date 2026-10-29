using System;
using GamePlatform.Backend.Contracts.Remote;
namespace GamePlatform.Features.Store
{
    public sealed class StorefrontOperationException : Exception
    {
        public StorefrontOperationException(RemoteFailure failure, string resultCode) : base(resultCode)
        {
            Failure = failure;
            ResultCode = resultCode;
        }
        public RemoteFailure Failure { get; }
        public string ResultCode { get; }
    }
}
