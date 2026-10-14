using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public interface IProvisioningRemote
    {
        Task<RemoteResult<ProvisioningSnapshot>> ProvisionAsync(AppId appId, Guid installationId, ClientStreamId streamId, CancellationToken cancellationToken);
    }
}
