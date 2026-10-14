using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Storage.Abstractions
{

    public interface ILocalStorageTransaction
    {
        StorageScope Scope { get; }
    }
}
