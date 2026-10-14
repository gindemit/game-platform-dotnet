using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GamePlatform.Configuration.Client.Tests
{
    public sealed class FileStoreTests
    {
        [Fact]
        public async Task Overlapping_real_file_writers_conflict_and_cancel_before_replace_preserves_index()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var space = new CacheNamespace(fixture.Scope, fixture.Owner);
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var faults = new FileFaults { Observe = boundary => { if (boundary == "before_commit") { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new IOException("owned_test_barrier_timeout"); } } };
            var first = new FileConfigurationStore(directory.Path, faults);
            var second = new FileConfigurationStore(directory.Path);
            var writing = Task.Run(() => first.CommitAsync(space, 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None));
            try
            {
                Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
                Assert.Equal(StoreCommitOutcome.Conflict, await second.CommitAsync(space, 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None));
            }
            finally { release.Set(); }
            Assert.Equal(StoreCommitOutcome.Committed, await writing);
            using var cancellation = new CancellationTokenSource();
            faults.Observe = boundary => { if (boundary == "before_commit") cancellation.Cancel(); };
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.CommitAsync(space, 1, fixture.Metadata("cache", 2), fixture.Objects("cache"), cancellation.Token));
            Assert.Equal(1, (await second.LoadAsync(space, CancellationToken.None))!.Generation);
        }
        [Fact]
        public async Task Real_files_commit_raw_bytes_restart_and_detect_corruption()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var space = new CacheNamespace(fixture.Scope, fixture.Owner);
            Assert.Equal(StoreCommitOutcome.Committed, await store.CommitAsync(space, 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None));
            var reopened = new FileConfigurationStore(directory.Path);
            var snapshot = await reopened.LoadAsync(space, CancellationToken.None);
            Assert.Equal(1, snapshot!.Generation);
            string sha = fixture.Example("cache")["normal"]["pointerRepresentationSha"].AsString();
            Assert.Equal(fixture.Documents["current.json"], await reopened.ReadObjectAsync(space, sha, 1048576, CancellationToken.None));
            await File.WriteAllTextAsync(Path.Combine(directory.Path, space.Key, "objects", sha + ".json"), "corrupt");
            await Assert.ThrowsAsync<InvalidConfigurationException>(() => reopened.ReadObjectAsync(space, sha, 1048576, CancellationToken.None));
        }
        [Theory]
        [InlineData("before_objects")]
        [InlineData("after_objects")]
        [InlineData("before_commit")]
        public async Task Interrupted_write_preserves_last_durable_generation(string boundary)
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var faults = new FileFaults();
            var store = new FileConfigurationStore(directory.Path, faults);
            var space = new CacheNamespace(fixture.Scope, fixture.Owner);
            Assert.Equal(StoreCommitOutcome.Committed, await store.CommitAsync(space, 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None));
            faults.Boundary = boundary;
            faults.Remaining = 1;
            Assert.Equal(StoreCommitOutcome.Unavailable, await store.CommitAsync(space, 1, fixture.Metadata("cache", 2), fixture.Objects("cache"), CancellationToken.None));
            Assert.Equal(1, (await store.LoadAsync(space, CancellationToken.None))!.Generation);
        }
        [Fact]
        public async Task Lost_ack_reports_unknown_and_actual_commit_is_recoverable()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var faults = new FileFaults { Boundary = "after_commit", Remaining = 1 };
            var store = new FileConfigurationStore(directory.Path, faults);
            var space = new CacheNamespace(fixture.Scope, fixture.Owner);
            Assert.Equal(StoreCommitOutcome.Unknown, await store.CommitAsync(space, 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None));
            Assert.Equal(1, (await store.LoadAsync(space, CancellationToken.None))!.Generation);
        }
        [Fact]
        public async Task Cas_and_namespace_binding_prevent_lost_updates_and_relabeling()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var a = new FileConfigurationStore(directory.Path);
            var b = new FileConfigurationStore(directory.Path);
            var space = new CacheNamespace(fixture.Scope, fixture.Owner);
            Assert.Equal(StoreCommitOutcome.Committed, await a.CommitAsync(space, 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None));
            Assert.Equal(StoreCommitOutcome.Conflict, await b.CommitAsync(space, 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None));
            var other = new CacheNamespace(fixture.Scope, new ConfigurationOwner("other", "other", 2));
            await Assert.ThrowsAsync<InvalidConfigurationException>(() => b.CommitAsync(other, 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None));
            Assert.Null(await b.LoadAsync(other, CancellationToken.None));
            Assert.NotNull(await b.LoadAsync(space, CancellationToken.None));
        }
        [Fact]
        public async Task Corrupt_index_falls_back_without_deleting_pins_or_objects()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var space = new CacheNamespace(fixture.Scope, fixture.Owner);
            await store.CommitAsync(space, 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None);
            await store.CommitAsync(space, 1, fixture.Metadata("cache", 2), fixture.Objects("cache"), CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(directory.Path, space.Key, "index.json"), "{partial");
            Assert.Equal(1, (await store.LoadAsync(space, CancellationToken.None))!.Generation);
            Assert.True(await store.ReadObjectAsync(space, fixture.Objects("cache").First().Key, 1048576, CancellationToken.None) != null);
        }
        [Fact]
        public async Task Public_namespace_rejects_private_pins_and_cancelled_commit_writes_nothing()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var space = new CacheNamespace(fixture.Scope, null);
            await Assert.ThrowsAsync<InvalidConfigurationException>(() => store.CommitAsync(space, 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CommitAsync(space, 0, fixture.Metadata("publicCache", 1), fixture.Objects("publicCache"), cancelled.Token));
            Assert.Null(await store.LoadAsync(space, CancellationToken.None));
        }
    }
}
