using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts;

namespace GamePlatform.Features
{
    public abstract class UnavailableFeature : IFeatureCapability
    {
        protected UnavailableFeature(string featureId) => FeatureId = featureId ?? throw new ArgumentNullException(nameof(featureId));
        public string FeatureId { get; }
        public FeatureStatus Status => FeatureStatus.Stubbed;
        public Task<PlatformResult<string>> ExecuteUnavailableAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(PlatformResult<string>.Error(PlatformFailure.NotImplemented, $"{FeatureId} is an M0 stub."));
        }
    }
}
