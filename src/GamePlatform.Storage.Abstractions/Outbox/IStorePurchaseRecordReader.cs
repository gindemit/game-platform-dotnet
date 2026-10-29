using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
namespace GamePlatform.Storage.Abstractions.Outbox
{
    public interface IStorePurchaseRecordReader
    {
        Task<IReadOnlyList<StorePurchaseRecord>> ReadAsync(ScopedOwnerContext owner, CancellationToken cancellationToken);
    }
}
