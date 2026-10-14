using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public interface IAuthSession
    {
        string SessionKey { get; }
        Task<AccessTokenSnapshot> GetAsync(CancellationToken cancellationToken);
        Task<AccessTokenSnapshot> RefreshAsync(long rejectedGeneration, CancellationToken cancellationToken);
    }
}
