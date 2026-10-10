using System;
using System.Collections.Generic;
using System.Linq;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Tests.Wire
{
    public sealed class CoreDtoTests
    {
        [Fact]
        public void OptionalDistinguishesMissingNullAndValue()
        {
            var missing = WireOptional<string?>.Absent;
            var cleared = WireOptional<string?>.Present(null);
            var present = WireOptional<string?>.Present("avatar:v1");
            Assert.False(missing.IsPresent);
            Assert.Throws<InvalidOperationException>(() => missing.Value);
            Assert.True(cleared.IsPresent);
            Assert.Null(cleared.Value);
            Assert.Equal("avatar:v1", present.Value);
        }

        [Fact]
        public void ProfilePatchPreservesClearAndOmittedFields()
        {
            var patch = new ProfilePatchCommand(long.MaxValue, default, WireOptional<string?>.Present(null), default);
            Assert.Equal(long.MaxValue, patch.ExpectedRevision);
            Assert.True(patch.AvatarKey.IsPresent);
            Assert.Null(patch.AvatarKey.Value);
            Assert.False(patch.DisplayName.IsPresent);
            Assert.False(patch.Locale.IsPresent);
        }

        [Fact]
        public void GameplayPreservesConsumerStringsAndFullWidthValuesAndCopiesMetrics()
        {
            var metrics = new Dictionary<string, long> { ["actions / original"] = 9007199254740993L };
            var dto = new GameplayCompletionCommand(" level / original ", int.MaxValue, "mode / 1", "hard / 2", true,
                long.MinValue, new GameplayValidation("none", ""), default, "session / original", long.MaxValue, 1000000, metrics);
            metrics["actions / original"] = 0;
            Assert.Equal(" level / original ", dto.ContentId);
            Assert.Equal("session / original", dto.Session);
            Assert.Equal(long.MinValue, dto.Score);
            Assert.Equal(long.MaxValue, dto.DurationTicks);
            Assert.Equal(9007199254740993L, dto.Metrics["actions / original"]);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, long>)dto.Metrics).Add("x", 0));
        }

        [Fact]
        public void AccountCollectionsAreDefensiveAndRetainIssuedUuidVersions()
        {
            var id = Guid.Parse("a90c57e1-8b8d-4f26-90ff-930671e8922c");
            var membership = new CommonMembership(id, "active", 0, long.MaxValue);
            var source = new[] { membership };
            var dto = new AccountResponse(new CommonAccount(id, 0, 0), source, 253402300799999L);
            source[0] = new CommonMembership(Guid.NewGuid(), "revoked", 1, 1);
            Assert.Same(membership, dto.Memberships[0]);
            Assert.Equal(id, dto.Account.PlatformUserId);
            Assert.Equal(253402300799999L, dto.ServerTime);
            Assert.Throws<NotSupportedException>(() => ((IList<CommonMembership>)dto.Memberships).Clear());
        }

        [Fact]
        public void ExhaustedStreamAndAbsentReceiptRepresentNullWithoutInventingSequence()
        {
            var id = Guid.Parse("019f4170-7000-7000-8000-000000000001");
            var stream = new CommonStreamState(id, id, long.MaxValue, null, "retired");
            var receipt = new RecoveryReceiptResponse(id, false, null, 0, id, long.MaxValue);
            Assert.Null(stream.NextSequence);
            Assert.Null(receipt.Result);
            Assert.False(receipt.Found);
            Assert.Equal(long.MaxValue, receipt.ObservedFinalizedThrough);
        }

        [Fact]
        public void ExtensionTreesCopyEveryContainerAndHaveNoNumericVariant()
        {
            var array = new[] { ExtensionValue.FromString("original") };
            var items = ExtensionValue.FromArray(array);
            var source = new Dictionary<string, ExtensionValue> { ["nested"] = items };
            var tree = ExtensionValue.FromObject(source);
            array[0] = ExtensionValue.Null;
            source.Clear();
            Assert.Equal("original", tree.Properties!["nested"].Items![0].Text);
            Assert.Equal(5, Enum.GetValues<ExtensionValueKind>().Length);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, ExtensionValue>)tree.Properties!).Clear());
        }

        [Fact]
        public void ErrorDetailsPreserveNullAndInt32AndCopyOptionalMap()
        {
            var source = new Dictionary<string, ErrorDetailValue> { ["limit"] = ErrorDetailValue.FromInteger(int.MinValue), ["missing"] = ErrorDetailValue.Null };
            var dto = new CommonError("invalid", "validation", "Invalid input", false, default,
                WireOptional<IReadOnlyDictionary<string, ErrorDetailValue>>.Present(source));
            source.Clear();
            Assert.Equal(int.MinValue, dto.Details.Value["limit"].Integer);
            Assert.Equal(ErrorDetailKind.Null, dto.Details.Value["missing"].Kind);
            Assert.False(dto.RetryAfterMilliseconds.IsPresent);
        }

        [Fact]
        public void ProjectionBranchesCarryDistinctKeysAndRemovalHasNoData()
        {
            IProjectionChange upsert = new ProjectionWalletUpsert(new ProjectionWalletKey("gold"), 7, new ProjectionWalletData(long.MaxValue));
            IProjectionChange removal = new ProjectionWalletRemoval(new ProjectionWalletKey("gold"), 8, "view_remove");
            Assert.Equal("upsert", ((ProjectionWalletUpsert)upsert).Kind);
            Assert.Equal("wallet", ((ProjectionWalletRemoval)removal).EntityType);
            Assert.Null(removal.GetType().GetProperty("Data"));
        }

        [Fact]
        public void SnapshotAndPullConstantsKeepBranchMeaning()
        {
            var collection = new BootstrapCollection("wallet");
            var page = new BootstrapPageResponse("snapshot", 2, Array.Empty<IProjectionSnapshotEntity>(), false, null, "cursor", 0);
            var reset = new PullReset(Array.Empty<IProjectionChange>(), null, "visibility_changed", 0);
            Assert.True(collection.Required);
            Assert.Equal(1, collection.SchemaVersion);
            Assert.Null(page.NextPageToken);
            Assert.Equal("cursor", page.InitialPullCursor);
            Assert.True(reset.ResetRequired);
            Assert.False(reset.HasMore);
        }

        [Fact]
        public void GeneratedDtosHaveNoPublicSettersOrSerializationAttributesOrRuntimeDependencies()
        {
            var assembly = typeof(ProvisionRequest).Assembly;
            var carriers = assembly.GetExportedTypes().Where(t => t.IsClass && t.IsSealed && t != typeof(ExtensionValue) && t != typeof(ProtocolVersion)).ToArray();
            Assert.Equal(77, carriers.Count(t => t.Namespace != "GamePlatform.Wire.Contracts.Teams"));
            Assert.Equal(12, carriers.Count(t => t.Namespace == "GamePlatform.Wire.Contracts.Teams"));
            foreach (var carrier in carriers)
            {
                foreach (var property in carrier.GetProperties()) Assert.Null(property.SetMethod);
                Assert.DoesNotContain(carrier.CustomAttributes, a => a.AttributeType.Namespace?.StartsWith("MessagePack", StringComparison.Ordinal) == true);
            }
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name?.StartsWith("GamePlatform.", StringComparison.Ordinal) == true);
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name?.Contains("MessagePack", StringComparison.Ordinal) == true);
        }

        [Fact]
        public void RequiredReferenceFieldsRejectNull()
        {
            Assert.Throws<ArgumentNullException>(() => new BootstrapPageRequest(null!, "page", 65536));
            Assert.Throws<ArgumentNullException>(() => new AccountResponse(null!, Array.Empty<CommonMembership>(), 0));
            Assert.Throws<ArgumentNullException>(() => new AccountResponse(new CommonAccount(Guid.Empty, 0, 0), null!, 0));
        }
    }
}
