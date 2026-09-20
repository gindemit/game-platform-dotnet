#nullable enable
using System;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Serialization.MessagePack;

namespace GamePlatform.Transport.Http
{
    /// <summary>
    /// Explicit production MessagePack composition for only the implemented
    /// provisioning, private bootstrap/pull and receipt providers. The caller
    /// owns and quiesces the executor, auth session and refresh coordinator.
    /// </summary>
    public sealed class ProductionBackendHttpProviders
    {
        public ProductionBackendHttpProviders(
            BackendHttpConfiguration configuration,
            BoundedHttpClientExecutor executor,
            IAuthSession auth,
            AuthRefreshCoordinator refresh)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            codec = new MessagePackWireCodec();
            Provisioning = new ProvisioningHttpProvider(configuration, executor, codec, auth, refresh);
        }

        private readonly BackendHttpConfiguration configuration;
        private readonly BoundedHttpClientExecutor executor;
        private readonly IAuthSession auth;
        private readonly AuthRefreshCoordinator refresh;
        private readonly MessagePackWireCodec codec;
        public IProvisioningRemote Provisioning { get; }

        public ProductionAccountHttpProviders ForAccount(AppId appId, PlatformUserId accountId, Guid installationId) =>
            new ProductionAccountHttpProviders(appId, accountId, installationId, configuration, executor, codec, auth, refresh);
    }

    /// <summary>Immutable account-owned portion created only after provisioning.</summary>
    public sealed class ProductionAccountHttpProviders
    {
        internal ProductionAccountHttpProviders(
            AppId appId,
            PlatformUserId accountId,
            Guid installationId,
            BackendHttpConfiguration configuration,
            BoundedHttpClientExecutor executor,
            MessagePackWireCodec codec,
            IAuthSession auth,
            AuthRefreshCoordinator refresh)
        {
            if (!appId.IsValid) throw new ArgumentException("A valid app ID is required.", nameof(appId));
            if (!accountId.IsValid) throw new ArgumentException("A valid account ID is required.", nameof(accountId));
            if (installationId == Guid.Empty) throw new ArgumentException("A valid installation ID is required.", nameof(installationId));
            PrivateSync = new PrivateSyncHttpProvider(appId, accountId, configuration, executor, codec, auth, refresh);
            CommandReceipts = new CommandReceiptHttpProvider(appId, accountId, installationId, configuration, executor, codec, auth, refresh);
        }

        public IPrivateSyncRemote PrivateSync { get; }
        public ICommandReceiptRemote CommandReceipts { get; }
    }
}
