using System;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Transport.Abstractions;

namespace GamePlatform.Transport.Http
{
    /// <summary>
    /// Explicit production MessagePack composition for only the implemented
    /// provisioning, private bootstrap/pull, command-push and receipt providers. The caller
    /// owns and quiesces the executor, auth session and refresh coordinator.
    /// </summary>
    public sealed class ProductionBackendHttpProviders
    {
        public ProductionBackendHttpProviders(
            BackendHttpConfiguration configuration,
            IHttpExecutor executor,
            IAuthSession auth,
            AuthRefreshCoordinator refresh)
            : this(configuration, executor, auth, refresh, new StoreQuestCapability(false))
        {
        }

        /// <summary>
        /// Derives the command fingerprint and every account push/receipt provider from one store/quest capability.
        /// </summary>
        public ProductionBackendHttpProviders(
            BackendHttpConfiguration configuration,
            IHttpExecutor executor,
            IAuthSession auth,
            AuthRefreshCoordinator refresh,
            StoreQuestCapability storeQuest)
        {
            this.storeQuest = storeQuest ?? throw new ArgumentNullException(nameof(storeQuest));
            CommandFingerprint = storeQuest.CreateFingerprint();
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            codec = new MessagePackWireCodec();
            Provisioning = new ProvisioningHttpProvider(configuration, executor, codec, auth, refresh);
        }

        private readonly BackendHttpConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly IAuthSession auth;
        private readonly AuthRefreshCoordinator refresh;
        private readonly MessagePackWireCodec codec;
        private readonly StoreQuestCapability storeQuest;
        public IProvisioningRemote Provisioning { get; }
        public ICommandFingerprint CommandFingerprint { get; }

        public ProductionAccountHttpProviders ForAccount(AppId appId, PlatformUserId accountId, Guid installationId) =>
            new ProductionAccountHttpProviders(appId, accountId, installationId, configuration, executor, codec, auth, refresh, storeQuest);
    }
}
