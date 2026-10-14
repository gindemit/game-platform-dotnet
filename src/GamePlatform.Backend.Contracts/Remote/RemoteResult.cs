using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public readonly struct RemoteResult<T>
    {
        private RemoteResult(T value)
        {
            IsSuccess = true;
            Value = value;
            Failure = default;
        }
        private RemoteResult(RemoteFailure failure)
        {
            IsSuccess = false;
            Value = default;
            Failure = failure;
        }
        public bool IsSuccess { get; }
        public T? Value { get; }
        public RemoteFailure Failure { get; }
        public static RemoteResult<T> Success(T value) => new RemoteResult<T>(value ?? throw new ArgumentNullException(nameof(value)));
        public static RemoteResult<T> Failed(RemoteFailure failure) => new RemoteResult<T>(failure);
    }
}
