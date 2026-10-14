using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;

namespace GamePlatform.Features.Teams
{
    public interface ITeamsRemote
    {
        Task<RemoteResult<TeamsResponse>> QueryAsync(TeamsQuery query, CancellationToken cancellationToken);
        Task<RemoteResult<TeamsResponse>> ExecuteAsync(TeamsCommand command, CancellationToken cancellationToken);
    }
}
