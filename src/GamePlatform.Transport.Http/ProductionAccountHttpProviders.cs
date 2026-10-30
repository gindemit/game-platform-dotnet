using System;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Transport.Abstractions;

namespace GamePlatform.Transport.Http
{

    /// <summary>Immutable account-owned portion created only after provisioning.</summary>
    public sealed class ProductionAccountHttpProviders
    {
        internal ProductionAccountHttpProviders(
            AppId appId,
            PlatformUserId accountId,
            Guid installationId,
            BackendHttpConfiguration configuration,
            IHttpExecutor executor,
            MessagePackWireCodec codec,
            IAuthSession auth,
            AuthRefreshCoordinator refresh,
            StoreQuestCapability storeQuest)
        {
            if (!appId.IsValid)
                throw new ArgumentException("A valid app ID is required.", nameof(appId));
            if (!accountId.IsValid)
                throw new ArgumentException("A valid account ID is required.", nameof(accountId));
            if (installationId == Guid.Empty)
                throw new ArgumentException("A valid installation ID is required.", nameof(installationId));
            PrivateSync = new PrivateSyncHttpProvider(appId, accountId, configuration, executor, new FriendsSupportingWireCodec(), auth, refresh);
            Teams = new TeamsHttpProvider(appId, accountId, configuration, executor, auth, refresh);
            Friends = new FriendsHttpProvider(appId, accountId, configuration, executor, auth, refresh);
            Leaderboards = new LeaderboardsHttpProvider(appId, accountId, configuration, executor, auth, refresh);
            PublicProfiles = new PublicProfileHttpProvider(appId, accountId, configuration, executor, auth, refresh);
            Daily = new DailyHttpProvider(appId, accountId, configuration, executor, auth, refresh);
            Storefront = new StorefrontHttpProvider(appId, accountId, configuration, executor, auth, refresh);
            NativePurchases = new NativePurchaseHttpProvider(appId, accountId, configuration, executor, auth, refresh);
            CommandPush = new CommandPushHttpProvider(appId, accountId, configuration, executor, codec, auth, refresh, storeQuest);
            CommandReceipts = new CommandReceiptHttpProvider(appId, accountId, installationId, configuration, executor, codec, auth, refresh,
                BackendWireRepresentation.MessagePack, storeQuest);
            CommandStreamRecovery = new CommandStreamRecoveryHttpProvider(appId, accountId, installationId,
                configuration, executor, codec, auth, refresh);
            RewardReceipts = new G3RewardReceiptHttpProvider(appId, accountId, configuration, executor, new G3RewardReceiptMessagePackCodec(), codec, auth, refresh);
        }

        public IPrivateSyncRemote PrivateSync { get; }
        public TeamsHttpProvider Teams { get; }
        public FriendsHttpProvider Friends { get; }
        public LeaderboardsHttpProvider Leaderboards { get; }
        public PublicProfileHttpProvider PublicProfiles { get; }
        public DailyHttpProvider Daily { get; }
        public StorefrontHttpProvider Storefront { get; }
        public NativePurchaseHttpProvider NativePurchases { get; }
        public ICommandRemote CommandPush { get; }
        public ICommandReceiptRemote CommandReceipts { get; }
        public ICommandStreamRecoveryRemote CommandStreamRecovery { get; }
        public G3RewardReceiptHttpProvider RewardReceipts { get; }
    }
}
