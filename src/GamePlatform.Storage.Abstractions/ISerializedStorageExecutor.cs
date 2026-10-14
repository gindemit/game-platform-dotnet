using System;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Storage.Abstractions
{

    public interface ISerializedStorageExecutor
    {
        Task<T> ExecuteAsync<T>(
            StorageScope scope,
            Func<ILocalStorageTransaction, T> operation,
            CancellationToken cancellationToken);
    }
}
