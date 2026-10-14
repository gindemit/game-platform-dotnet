using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.FeatureState
{

    public interface IDurableFeatureStateStore
    {
        Task<DurableFeatureState?> ReadAsync(ScopedOwnerContext owner, string featureNamespace, string entityKey, CancellationToken cancellationToken);
        DurableFeatureState? Read(ILocalStorageTransaction transaction, ScopedOwnerContext owner, string featureNamespace, string entityKey);
        void Upsert(ILocalStorageTransaction transaction, DurableFeatureMutation mutation);
        void Delete(ILocalStorageTransaction transaction, ScopedOwnerContext owner, string featureNamespace, string entityKey);
    }
}
