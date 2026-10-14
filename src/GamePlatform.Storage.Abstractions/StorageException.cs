using System;

namespace GamePlatform.Storage.Abstractions
{

    public sealed class StorageException : Exception
    {
        public StorageException(StorageFailure failure, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Failure = failure;
        }

        public StorageFailure Failure { get; }
    }
}
