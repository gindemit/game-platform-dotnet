using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace GamePlatform.Configuration.Client.Tests
{
    [Collection("Retention allocation measurement")]
    public sealed class ConfigurationRetentionAllocationTests
    {
        private const string PinPrefix = "0199e584-6400-7000-8000-";
        private readonly ITestOutputHelper output;

        public ConfigurationRetentionAllocationTests(ITestOutputHelper output) { this.output = output; }

        private sealed class ObservedStore : IConfigurationStore
        {
            private readonly IConfigurationStore inner;
            internal readonly List<string> Reads = new List<string>();
            internal string? DamagedSha;
            internal bool Missing;
            internal string? DamageOnConflictSha;
            internal int Conflicts;

            internal ObservedStore(IConfigurationStore inner) { this.inner = inner; }
            public Task<StoreSnapshot?> LoadAsync(CacheNamespace space, CancellationToken token) => inner.LoadAsync(space, token);
            public async Task<byte[]?> ReadObjectAsync(CacheNamespace space, string sha, int maximum, CancellationToken token)
            {
                Reads.Add(sha);
                if (sha == DamagedSha)
                    return Missing ? null : new byte[] { 1 };
                return await inner.ReadObjectAsync(space, sha, maximum, token);
            }
            public Task<StoreCommitOutcome> CommitAsync(CacheNamespace space, long expected, byte[] metadata,
                IReadOnlyDictionary<string, byte[]> added, CancellationToken token)
            {
                if (DamageOnConflictSha != null)
                {
                    token.ThrowIfCancellationRequested();
                    DamagedSha = DamageOnConflictSha;
                    DamageOnConflictSha = null;
                    Conflicts++;
                    return Task.FromResult(StoreCommitOutcome.Conflict);
                }
                return inner.CommitAsync(space, expected, metadata, added, token);
            }
        }

        [Fact]
        public async Task Retention_reads_one_manifest_per_origin_when_many_pins_share_it()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new ObservedStore(new FileConfigurationStore(directory.Path));
            using var client = fixture.Client(new FixtureTransport(fixture), store, new TestClock());
            var context = (await client.GetFeatureAsync("progression")).Feature!;
            for (int i = 1; i <= 3; i++)
                Assert.Equal("available", (await client.PinAsync(context, PinPrefix + i.ToString("D12"))).Status);
            store.Reads.Clear();

            var result = await client.PinAsync(context, PinPrefix + "000000000004");

            Assert.Equal("available", result.Status);
            Assert.True(result.Durable);
            Assert.Equal(1, store.Reads.Count(sha => sha == context.Origin.ManifestRepresentationSha));
            for (int i = 1; i <= 4; i++)
                Assert.Equal(context.Selection.Choice.Variant,
                    (await client.GetPinnedAsync(PinPrefix + i.ToString("D12"))).Feature!.Selection.Choice.Variant);
        }

        [Fact]
        public async Task Retention_checks_each_distinct_manifest_across_old_and_current_pins()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new ObservedStore(new FileConfigurationStore(directory.Path));
            using var client = fixture.Client(new FixtureTransport(fixture), store, new TestClock());
            var old = (await client.GetFeatureAsync("progression")).Feature!;
            await client.PinAsync(old, PinPrefix + "000000000001");
            fixture.AdvanceRelease();
            await client.RefreshAsync();
            var current = (await client.GetFeatureAsync("progression")).Feature!;
            Assert.NotEqual(old.Origin.ManifestRepresentationSha, current.Origin.ManifestRepresentationSha);
            store.Reads.Clear();

            Assert.Equal("available", (await client.PinAsync(current, PinPrefix + "000000000002")).Status);

            Assert.Equal(1, store.Reads.Count(sha => sha == old.Origin.ManifestRepresentationSha));
            Assert.Equal(1, store.Reads.Count(sha => sha == current.Origin.ManifestRepresentationSha));
            Assert.Equal(old.Selection.Choice.Variant, (await client.GetPinnedAsync(PinPrefix + "000000000001")).Feature!.Selection.Choice.Variant);
            Assert.Equal(current.Selection.Choice.Variant, (await client.GetPinnedAsync(PinPrefix + "000000000002")).Feature!.Selection.Choice.Variant);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Retention_revalidates_manifest_on_later_commit_and_rejects_missing_or_corrupt_bytes(bool missing)
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new ObservedStore(new FileConfigurationStore(directory.Path));
            using var client = fixture.Client(new FixtureTransport(fixture), store, new TestClock());
            var context = (await client.GetFeatureAsync("progression")).Feature!;
            await client.PinAsync(context, PinPrefix + "000000000001");
            var space = new CacheNamespace(fixture.Scope, fixture.Owner);
            var before = (await store.LoadAsync(space, CancellationToken.None))!;
            store.DamagedSha = context.Origin.ManifestRepresentationSha;
            store.Missing = missing;

            var result = await client.PinAsync(context, PinPrefix + "000000000002");

            Assert.Equal("preserved", result.Status);
            Assert.Equal("integrity", result.Reason);
            Assert.Equal(before.Metadata, (await store.LoadAsync(space, CancellationToken.None))!.Metadata);
            Assert.Null((await client.GetPinnedAsync(PinPrefix + "000000000002")).Feature);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Retention_revalidates_manifest_after_cas_conflict(bool missing)
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new ObservedStore(new FileConfigurationStore(directory.Path));
            using var client = fixture.Client(new FixtureTransport(fixture), store, new TestClock());
            var context = (await client.GetFeatureAsync("progression")).Feature!;
            await client.PinAsync(context, PinPrefix + "000000000001");
            var space = new CacheNamespace(fixture.Scope, fixture.Owner);
            var before = (await store.LoadAsync(space, CancellationToken.None))!;
            store.DamageOnConflictSha = context.Origin.ManifestRepresentationSha;
            store.Missing = missing;
            store.Reads.Clear();

            var result = await client.PinAsync(context, PinPrefix + "000000000002");

            Assert.Equal(1, store.Conflicts);
            Assert.Equal("preserved", result.Status);
            Assert.Equal("integrity", result.Reason);
            Assert.True(store.Reads.Count(sha => sha == context.Origin.ManifestRepresentationSha) >= 2);
            Assert.Equal(before.Metadata, (await store.LoadAsync(space, CancellationToken.None))!.Metadata);
            Assert.Null((await client.GetPinnedAsync(PinPrefix + "000000000002")).Feature);
        }

        [Fact]
        public async Task Retention_measurement_with_twenty_three_shared_pins()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new ObservedStore(new FileConfigurationStore(directory.Path));
            using var client = fixture.Client(new FixtureTransport(fixture), store, new TestClock());
            var context = (await client.GetFeatureAsync("progression")).Feature!;
            for (int i = 1; i <= 23; i++)
                Assert.Equal("available", (await client.PinAsync(context, PinPrefix + i.ToString("D12"))).Status);
            for (int i = 0; i < 3; i++)
                Assert.Equal("available", (await client.GetFeatureAsync("progression")).Status);
            store.Reads.Clear();

            const int iterations = 7;
            long before = GC.GetTotalAllocatedBytes(true);
            for (int i = 0; i < iterations; i++)
            {
                var selected = await client.GetFeatureAsync("progression");
                Assert.Equal("available", selected.Status);
                Assert.Equal("activated", selected.Reason);
                Assert.True(selected.Durable);
            }
            long allocated = GC.GetTotalAllocatedBytes(true) - before;
            int reads = store.Reads.Count(sha => sha == context.Origin.ManifestRepresentationSha);

            // Process-wide managed allocation measurement; run this case alone for comparison.
            // No byte threshold: host runtime/file IO overhead is not an Android allocation budget.
            output.WriteLine("23 retained pins, {0} reselections: {1} allocated bytes ({2} bytes/selection), {3} manifest reads",
                iterations, allocated, allocated / iterations, reads);
            // Reselection supplies the current manifest as pending bytes, so this measures
            // avoided decoding even though neither baseline nor optimized code reads it from disk.
            Assert.Equal(0, reads);
        }
    }

    [CollectionDefinition("Retention allocation measurement", DisableParallelization = true)]
    public sealed class RetentionAllocationCollection { }
}
