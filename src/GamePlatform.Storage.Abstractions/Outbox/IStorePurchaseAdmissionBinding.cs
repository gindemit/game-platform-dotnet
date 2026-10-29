using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions.Outbox
{
    /// <summary>Captures the private sync identity in the same transaction as purchase admission.</summary>
    public interface IStorePurchaseAdmissionBinding
    {
        void Stage(ILocalStorageTransaction transaction, ScopedOwnerContext owner, OperationId operationId, long localRevision);
    }
}
