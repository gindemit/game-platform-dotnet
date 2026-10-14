namespace GamePlatform.Storage.Abstractions
{

    public interface ILocalStorageTransaction
    {
        StorageScope Scope { get; }
    }
}
