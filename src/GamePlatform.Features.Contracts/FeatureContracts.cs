using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts
{
    public enum FeatureStatus { Stubbed, Implemented }

    public interface IFeatureCapability
    {
        string FeatureId { get; }
        FeatureStatus Status { get; }
        Task<PlatformResult<string>> ExecuteUnavailableAsync(CancellationToken cancellationToken);
    }
}
