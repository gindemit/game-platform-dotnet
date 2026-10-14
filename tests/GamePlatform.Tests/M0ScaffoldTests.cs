using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features;
using GamePlatform.Features.Contracts;
using GamePlatform.Serialization.Json;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Storage.Sqlite;
using GamePlatform.Transport.Http;
using Xunit;

namespace GamePlatform.Tests
{
    public sealed class M0ScaffoldTests
    {
        public static IEnumerable<object[]> Features()
        {
            yield return new object[] { new AccountsFeature() };
            yield return new object[] { new ProfilesFeature() };
            yield return new object[] { new CatalogFeature() };
            yield return new object[] { new InventoryFeature() };
            yield return new object[] { new WalletFeature() };
            yield return new object[] { new EntitlementsFeature() };
            yield return new object[] { new RewardFulfillmentFeature() };
            yield return new object[] { new ProgressionFeature() };
            yield return new object[] { new QuestsFeature() };
            yield return new object[] { new AchievementsFeature() };
            yield return new object[] { new StoreFeature() };
            yield return new object[] { new PurchasesFeature() };
            yield return new object[] { new LeaderboardsFeature() };
            yield return new object[] { new TeamsFeature() };
            yield return new object[] { new RemoteConfigFeature() };
            yield return new object[] { new InboxFeature() };
        }

        [Theory]
        [MemberData(nameof(Features))]
        public async Task EveryFeatureFailsClosed(IFeatureCapability feature)
        {
            PlatformResult<string> result = await feature.ExecuteUnavailableAsync(CancellationToken.None);
            Assert.Equal(FeatureStatus.Stubbed, feature.Status);
            Assert.False(result.Succeeded);
            Assert.Equal(PlatformFailure.NotImplemented, result.Failure);
        }

        [Fact]
        public void PlatformIdRetainsOrdinalCompatibilityShape()
        {
            Assert.Equal(new PlatformId("Player-A"), new PlatformId("Player-A"));
            Assert.NotEqual(new PlatformId("Player-A"), new PlatformId("player-a"));
        }

        [Fact]
        public void ErrorCannotMasqueradeAsSuccess()
        {
            Assert.Throws<ArgumentException>(() => PlatformResult<string>.Error(PlatformFailure.None));
        }

        [Fact]
        public void ProductionAdaptersAreExplicitlyUnavailable()
        {
            Assert.Throws<PlatformCapabilityUnavailableException>(() => new UnavailableSqliteStore().Open());
            Assert.Throws<PlatformCapabilityUnavailableException>(() => new UnavailableHttpTransport().EnsureAvailable());
            Assert.Throws<PlatformCapabilityUnavailableException>(() => new UnavailableMessagePackCodec().Encode("x"));
            Assert.Throws<PlatformCapabilityUnavailableException>(() => new UnavailableJsonCodec().Encode("x"));
        }
    }
}
