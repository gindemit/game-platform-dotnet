using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Reflection;
using GamePlatform.Configuration;
using GamePlatform.Configuration.Client;
using Xunit;

namespace GamePlatform.Configuration.Client.Tests
{
    public sealed class ClientTests
    {
        private const string Pin = "0199e584-6400-7000-8000-000000000001";
        [Fact]
        public async Task Stale_generation_pin_rejection_returns_no_context_and_preserves_current_generation_pin()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var clock = new TestClock();
            using var old = fixture.Client(new FixtureTransport(fixture), store, clock);
            var context = (await old.GetFeatureAsync("progression")).Feature!;
            await old.PinAsync(context, Pin);
            var owner2 = new ConfigurationOwner(fixture.Owner.OwnerId, fixture.Owner.SubjectId, 2);
            using var current = fixture.Client(new FixtureTransport(fixture), store, clock, owner2);
            await current.RestoreAsync();
            await current.RefreshAsync();
            var space = new CacheNamespace(fixture.Scope, owner2);
            var before = (await store.LoadAsync(space, CancellationToken.None))!.Metadata;
            var rejected = await old.PinAsync(context, "0199e584-6400-7000-8000-000000000002");
            Assert.Equal("rejected", rejected.Status);
            Assert.Equal("owner_changed", rejected.Reason);
            Assert.False(rejected.Durable);
            Assert.Null(rejected.Feature);
            Assert.Equal(before, (await store.LoadAsync(space, CancellationToken.None))!.Metadata);
            Assert.Equal("B", (await current.GetPinnedAsync(Pin)).Feature!.Selection.Choice.Variant);
        }
        [Fact]
        public async Task Same_generation_pin_storage_failure_preserves_valid_existing_context_without_claiming_new_pin()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var faults = new FileFaults();
            var store = new FileConfigurationStore(directory.Path, faults);
            using var client = fixture.Client(new FixtureTransport(fixture), store, new TestClock());
            var context = (await client.GetFeatureAsync("progression")).Feature!;
            faults.Remaining = 1;
            var failed = await client.PinAsync(context, Pin);
            Assert.Equal("preserved", failed.Status);
            Assert.Equal("storage_failure", failed.Reason);
            Assert.True(failed.Durable);
            Assert.Same(context, failed.Feature);
            Assert.Null((await client.GetPinnedAsync(Pin)).Feature);
            var snapshot = (await store.LoadAsync(new CacheNamespace(fixture.Scope, fixture.Owner), CancellationToken.None))!;
            Assert.Empty(CanonicalJson.Parse(snapshot.Metadata)["pins"].AsArray());
        }
        [Fact]
        public async Task Higher_same_owner_generation_fences_late_graph_and_preserves_saved_pin()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var clock = new TestClock();
            var transport = new FixtureTransport(fixture);
            using var old = fixture.Client(transport, store, clock);
            var prior = (await old.GetFeatureAsync("progression")).Feature!;
            await old.PinAsync(prior, Pin);
            fixture.AdvanceRelease();
            await old.RefreshAsync();
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            string a = fixture.Example("manifest")["assets"].AsArray().Single(v => v["revision"].AsString() == "A-1")["path"].AsString();
            transport.Override = async (request, token) => { string path = request.Address.AbsolutePath.TrimStart('/'); if (path == a) { entered.TrySetResult(true); await release.Task.WaitAsync(token); } return new ConfigurationHttpResponse(200, fixture.Documents[path], request.Address); };
            var late = old.GetFeatureAsync("progression");
            await entered.Task;
            var nextOwner = new ConfigurationOwner(fixture.Owner.OwnerId, fixture.Owner.SubjectId, 2);
            using var current = fixture.Client(new FixtureTransport(fixture), store, clock, nextOwner);
            await current.RestoreAsync();
            await current.RefreshAsync();
            release.SetResult(true);
            var rejected = await late;
            Assert.Equal("owner_changed", rejected.Reason);
            Assert.Null(rejected.Feature);
            Assert.False(rejected.Durable);
            Assert.Equal("B", (await current.GetPinnedAsync(Pin)).Feature!.Selection.Choice.Variant);
            await Assert.ThrowsAsync<InvalidConfigurationException>(() => old.GetPinnedAsync(Pin));
            var metadata = CanonicalJson.Parse((await store.LoadAsync(new CacheNamespace(fixture.Scope, nextOwner), CancellationToken.None))!.Metadata);
            Assert.Equal("2", metadata["owner"]["generation"].AsString());
        }
        [Fact]
        public async Task Cas_retry_rechecks_owner_generation_and_store_rejects_direct_regression()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var real = new FileConfigurationStore(directory.Path);
            var wrapped = new BeforeCommitStore(real);
            var clock = new TestClock();
            using var old = fixture.Client(new FixtureTransport(fixture), wrapped, clock);
            var feature = (await old.GetFeatureAsync("progression")).Feature!;
            await old.PinAsync(feature, Pin);
            var nextOwner = new ConfigurationOwner(fixture.Owner.OwnerId, fixture.Owner.SubjectId, 2);
            using var current = fixture.Client(new FixtureTransport(fixture), real, clock, nextOwner);
            await current.RestoreAsync();
            wrapped.BeforeNextCommit = async () => { Assert.Equal("fresh", (await current.RefreshAsync()).Reason); };
            var rejected = await old.GetFeatureAsync("progression");
            Assert.Equal(1, wrapped.Conflicts);
            Assert.Equal("owner_changed", rejected.Reason);
            Assert.Null(rejected.Feature);
            Assert.Equal("B", (await current.GetPinnedAsync(Pin)).Feature!.Selection.Choice.Variant);
            var space = new CacheNamespace(fixture.Scope, nextOwner);
            var snapshot = (await real.LoadAsync(space, CancellationToken.None))!;
            var metadata = CanonicalJson.Parse(snapshot.Metadata);
            var stale = Fixture.Set(Fixture.Set(metadata, "storeGeneration", JsonValue.String((snapshot.Generation + 1).ToString())), "owner", Fixture.Set(metadata["owner"], "generation", JsonValue.String("1")));
            await Assert.ThrowsAsync<InvalidConfigurationException>(() => real.CommitAsync(new CacheNamespace(fixture.Scope, fixture.Owner), snapshot.Generation, CanonicalJson.Serialize(stale), new Dictionary<string, byte[]>(), CancellationToken.None));
            Assert.Equal(snapshot.Metadata, (await real.LoadAsync(space, CancellationToken.None))!.Metadata);
        }
        [Fact]
        public async Task Restore_rejects_percentage_activation_and_pin_against_disabled_retained_selector()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var clock = new TestClock();
            var space = new CacheNamespace(fixture.Scope, fixture.Owner);
            using (var client = fixture.Client(new FixtureTransport(fixture), store, clock))
            {
                var context = (await client.GetFeatureAsync("progression")).Feature!;
                await client.PinAsync(context, Pin);
            }
            var current = (await store.LoadAsync(space, CancellationToken.None))!;
            var metadata = CanonicalJson.Parse(current.Metadata);
            var selector = fixture.Corpus["fixtureDocuments"]["selector"];
            selector = Fixture.Set(selector, "features", JsonValue.Array(selector["features"].AsArray().Select(f => Fixture.Set(f, "enabled", JsonValue.Boolean(false)))));
            byte[] selectorBytes = CanonicalJson.Serialize(selector);
            string selectorSha = CanonicalJson.Sha256(selectorBytes);
            var selectorRef = Fixture.Object(("path", JsonValue.String("selectors/" + selectorSha + ".json")), ("representationSha", JsonValue.String(selectorSha)), ("semanticSha", JsonValue.String(selectorSha)), ("decodedBytes", JsonValue.Integer(selectorBytes.Length)));
            var manifest = Fixture.Set(fixture.Example("manifest"), "selector", selectorRef);
            byte[] manifestBytes = CanonicalJson.Serialize(manifest);
            string manifestSha = CanonicalJson.Sha256(manifestBytes);
            var added = new Dictionary<string, byte[]> { [selectorSha] = selectorBytes, [manifestSha] = manifestBytes };
            var origin = Fixture.Object(("releaseSequence", JsonValue.String("7")), ("manifestRepresentationSha", JsonValue.String(manifestSha)), ("selectorRepresentationSha", JsonValue.String(selectorSha)));
            metadata = Fixture.Set(metadata, "normal", JsonValue.Null);
            metadata = Fixture.Set(metadata, "storeGeneration", JsonValue.String((current.Generation + 1).ToString()));
            metadata = Fixture.Set(metadata, "activations", JsonValue.Array(metadata["activations"].AsArray().Select(a => Fixture.Set(a, "origin", origin))));
            metadata = Fixture.Set(metadata, "pins", JsonValue.Array(metadata["pins"].AsArray().Select(p => Fixture.Set(p, "activation", Fixture.Set(p["activation"], "origin", origin)))));
            metadata = Fixture.Set(metadata, "objects", JsonValue.Array(metadata["objects"].AsArray().Concat(added.Select(p => Fixture.Object(("representationSha", JsonValue.String(p.Key)), ("decodedBytes", JsonValue.Integer(p.Value.Length)), ("lastUsedAtSeconds", JsonValue.Integer(clock.UtcSeconds)))))));
            Assert.Equal(StoreCommitOutcome.Committed, await store.CommitAsync(space, current.Generation, CanonicalJson.Serialize(metadata), added, CancellationToken.None));
            using var restored = fixture.Client(new FixtureTransport(fixture), store, clock);
            Assert.False((await restored.RestoreAsync()).Durable);
            Assert.Null((await restored.GetPinnedAsync(Pin)).Feature);
            Assert.Contains(restored.Diagnostics, d => d.Reason == "graph_invalid");
        }
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Clock_freshness_lost_during_selected_download_never_admits_experiment(bool rollback)
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var clock = new TestClock();
            var transport = new FixtureTransport(fixture);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            string b = fixture.Example("manifest")["assets"].AsArray().Single(a => a["revision"].AsString() == "B-1")["path"].AsString();
            transport.Override = async (request, token) => { string path = request.Address.AbsolutePath.TrimStart('/'); if (path == b) { entered.TrySetResult(true); await release.Task.WaitAsync(token); } return new ConfigurationHttpResponse(200, fixture.Documents[path], request.Address); };
            using var client = fixture.Client(transport, store, clock);
            var pending = client.GetFeatureAsync("progression");
            await entered.Task;
            if (rollback)
                clock.UtcSeconds--;
            else
                clock.IsTrusted = false;
            release.SetResult(true);
            var result = await pending;
            Assert.Equal("unknown_time", result.Reason);
            Assert.Null(result.Feature);
            Assert.False(result.Durable);
            var index = CanonicalJson.Parse((await store.LoadAsync(new CacheNamespace(fixture.Scope, fixture.Owner), CancellationToken.None))!.Metadata);
            Assert.Empty(index["activations"].AsArray());
        }
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Clock_freshness_lost_during_store_wait_never_commits_experiment(bool rollback)
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var realStore = new FileConfigurationStore(directory.Path);
            var store = new BlockingLoadStore(realStore);
            var clock = new TestClock();
            using var client = fixture.Client(new FixtureTransport(fixture), store, clock);
            await client.RefreshAsync();
            store.Armed = true;
            var pending = client.GetFeatureAsync("progression");
            await store.Entered.Task;
            if (rollback)
                clock.UtcSeconds--;
            else
                clock.IsTrusted = false;
            store.Release.SetResult(true);
            var result = await pending;
            Assert.Null(result.Feature);
            Assert.Equal("unknown_time", result.Reason);
            Assert.False(result.Durable);
            var index = CanonicalJson.Parse((await realStore.LoadAsync(new CacheNamespace(fixture.Scope, fixture.Owner), CancellationToken.None))!.Metadata);
            Assert.Empty(index["activations"].AsArray());
        }
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Queued_pin_read_is_fenced_by_retirement_or_disposal(bool retire)
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            await store.CommitAsync(new CacheNamespace(fixture.Scope, fixture.Owner), 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None);
            using var client = fixture.Client(new FixtureTransport(fixture), store, new TestClock());
            await client.RestoreAsync();
            var gate = (SemaphoreSlim)typeof(ConfigurationClient).GetField("featureGate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
            await gate.WaitAsync();
            var pending = client.GetPinnedAsync(Pin);
            Assert.False(pending.IsCompleted);
            if (retire)
                client.Retire();
            else
                client.Dispose();
            gate.Release();
            await Assert.ThrowsAnyAsync<Exception>(() => pending);
        }
        [Theory]
        [InlineData("configuration.newInstance")]
        [InlineData("configuration.newEnrollment")]
        public async Task Missing_normal_metadata_cannot_bypass_admission_deny_using_previous_activation(string purpose)
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var metadata = Fixture.Set(CanonicalJson.Parse(fixture.Metadata("cache", 1)), "normal", JsonValue.Null);
            await store.CommitAsync(new CacheNamespace(fixture.Scope, fixture.Owner), 0, CanonicalJson.Serialize(metadata), fixture.Objects("cache"), CancellationToken.None);
            var transport = new FixtureTransport(fixture);
            transport.Override = (request, _) => { bool control = request.Address.AbsolutePath.EndsWith("control.json", StringComparison.Ordinal); return Task.FromResult(new ConfigurationHttpResponse(control ? 200 : 404, control ? fixture.Documents["control.json"] : Array.Empty<byte>(), request.Address)); };
            using var client = fixture.Client(transport, store, new TestClock());
            await client.RestoreAsync();
            fixture.AdmissionControl(12, purpose);
            await client.RefreshControlsAsync();
            Assert.Equal("B", (await client.GetPinnedAsync(Pin)).Feature!.Selection.Choice.Variant);
            var result = await client.GetFeatureAsync("progression");
            Assert.Null(result.Feature);
            Assert.False(result.Durable);
            Assert.NotNull(result.BundledChoice);
        }
        [Fact]
        public async Task Soft_budget_evicts_old_unreferenced_objects_and_preserves_pin_and_retired_namespace()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var objects = fixture.Objects("cache");
            var descriptors = fixture.Example("cache")["objects"].AsArray().ToList();
            var unused = new List<string>();
            for (int i = 0; i < 33; i++)
            {
                var content = Enumerable.Repeat((byte)32, 1048576).ToArray();
                Encoding.UTF8.GetBytes("{\"unused\":" + i + "}").CopyTo(content, 0);
                string sha = CanonicalJson.Sha256(content);
                objects[sha] = content;
                unused.Add(sha);
                descriptors.Add(Fixture.Object(("representationSha", JsonValue.String(sha)), ("decodedBytes", JsonValue.Integer(content.Length)), ("lastUsedAtSeconds", JsonValue.Integer(i))));
            }
            var metadata = Fixture.Set(Fixture.Set(fixture.Example("cache"), "objects", JsonValue.Array(descriptors)), "storeGeneration", JsonValue.String("1"));
            var space = new CacheNamespace(fixture.Scope, fixture.Owner);
            Assert.Equal(StoreCommitOutcome.Committed, await store.CommitAsync(space, 0, CanonicalJson.Serialize(metadata), objects, CancellationToken.None));
            var retiredOwner = new ConfigurationOwner("retired-owner", fixture.Owner.SubjectId, 2);
            var retiredSpace = new CacheNamespace(fixture.Scope, retiredOwner);
            var retiredMetadata = Fixture.Set(Fixture.Set(fixture.Example("cache"), "owner", Fixture.Set(Fixture.Set(fixture.Example("cache")["owner"], "ownerId", JsonValue.String(retiredOwner.OwnerId)), "generation", JsonValue.String("2"))), "storeGeneration", JsonValue.String("1"));
            Assert.Equal(StoreCommitOutcome.Committed, await store.CommitAsync(retiredSpace, 0, CanonicalJson.Serialize(retiredMetadata), fixture.Objects("cache"), CancellationToken.None));
            var retiredBefore = (await store.LoadAsync(retiredSpace, CancellationToken.None))!.Metadata;
            using var client = fixture.Client(new FixtureTransport(fixture), store, new TestClock());
            await client.RestoreAsync();
            await client.RefreshAsync();
            var after = CanonicalJson.Parse((await store.LoadAsync(space, CancellationToken.None))!.Metadata);
            Assert.True(after["objects"].AsArray().Sum(v => v["decodedBytes"].AsInteger()) <= 32L * 1048576);
            Assert.DoesNotContain(after["objects"].AsArray(), v => v["representationSha"].AsString() == unused[0]);
            Assert.Equal("B", (await client.GetPinnedAsync(Pin)).Feature!.Selection.Choice.Variant);
            Assert.Equal(retiredBefore, (await store.LoadAsync(retiredSpace, CancellationToken.None))!.Metadata);
        }
        [Fact]
        public async Task Cold_metadata_and_selected_graph_share_one_immediate_retry_allowance()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var clock = new TestClock();
            var transport = new FixtureTransport(fixture);
            int pointers = 0;
            transport.Override = (request, _) => { string path = request.Address.AbsolutePath.TrimStart('/'); bool fail = (path == "current.json" && pointers++ == 0) || path.StartsWith("assets/", StringComparison.Ordinal); return Task.FromResult(new ConfigurationHttpResponse(fail ? 503 : 200, fail ? Array.Empty<byte>() : fixture.Documents[path], request.Address)); };
            using var client = fixture.Client(transport, new FileConfigurationStore(directory.Path), clock);
            var pending = client.GetFeatureAsync("progression");
            await clock.ShortDelayEntered.Task;
            clock.Advance(4);
            var result = await pending;
            Assert.False(result.Durable);
            Assert.Equal(2, pointers);
            Assert.All(transport.Requests.Where(p => p.StartsWith("assets/", StringComparison.Ordinal)).GroupBy(p => p), group => Assert.Single(group));
        }
        [Fact]
        public async Task Selected_graph_over_8_MiB_is_rejected_before_any_payload_request()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var transport = new FixtureTransport(fixture);
            var refs = Enumerable.Range(0, 8).Select(i => Fixture.Object(("key", JsonValue.String("zz" + i.ToString("D2"))), ("revision", JsonValue.String("1")), ("semanticSha", JsonValue.String(new string('a', 64))))).ToArray();
            var dependencies = JsonValue.Array(refs);
            var extra = refs.Select(v => Fixture.Object(("key", v["key"]), ("revision", v["revision"]), ("semanticSha", v["semanticSha"]), ("path", JsonValue.String("assets/" + CanonicalJson.SemanticSha256(JsonValue.Array(new[] { v["key"], v["revision"] })) + ".json")), ("representationSha", JsonValue.String(new string('b', 64))), ("decodedBytes", JsonValue.Integer(1048576)), ("dependencies", JsonValue.Array(Array.Empty<JsonValue>())))).ToArray();
            var selector = fixture.Corpus["fixtureDocuments"]["selector"];
            var feature = selector["features"].AsArray()[0];
            var rule = feature["rules"].AsArray()[0];
            var outcome = rule["outcome"];
            var allocations = outcome["allocations"].AsArray().Select(a => a["choice"]["variant"].AsString() == "B" ? Fixture.Set(a, "choice", Fixture.Set(a["choice"], "dependencies", dependencies)) : a);
            rule = Fixture.Set(rule, "outcome", Fixture.Set(outcome, "allocations", JsonValue.Array(allocations)));
            feature = Fixture.Set(feature, "rules", JsonValue.Array(new[] { rule }));
            selector = Fixture.Set(selector, "features", JsonValue.Array(new[] { feature }));
            var selectorBytes = CanonicalJson.Serialize(selector);
            string selectorSha = CanonicalJson.Sha256(selectorBytes);
            var manifest = fixture.Example("manifest");
            var selectorRef = Fixture.Set(Fixture.Set(Fixture.Set(Fixture.Set(manifest["selector"], "path", JsonValue.String("selectors/" + selectorSha + ".json")), "representationSha", JsonValue.String(selectorSha)), "semanticSha", JsonValue.String(selectorSha)), "decodedBytes", JsonValue.Integer(selectorBytes.Length));
            manifest = Fixture.Set(manifest, "selector", selectorRef);
            manifest = Fixture.Set(manifest, "assets", JsonValue.Array(manifest["assets"].AsArray().Select(a => a["revision"].AsString() == "B-1" ? Fixture.Set(a, "dependencies", dependencies) : a).Concat(extra)));
            manifest = Fixture.Set(manifest, "retained", JsonValue.Array(manifest["retained"].AsArray().Concat(refs)));
            var manifestBytes = CanonicalJson.Serialize(manifest);
            string manifestSha = CanonicalJson.Sha256(manifestBytes);
            var manifestRef = Fixture.Set(Fixture.Set(Fixture.Set(Fixture.Set(fixture.Example("current")["manifest"], "path", JsonValue.String("manifests/" + manifestSha + ".json")), "representationSha", JsonValue.String(manifestSha)), "semanticSha", JsonValue.String(manifestSha)), "decodedBytes", JsonValue.Integer(manifestBytes.Length));
            fixture.Documents[selectorRef["path"].AsString()] = selectorBytes;
            fixture.Documents[manifestRef["path"].AsString()] = manifestBytes;
            fixture.Documents["current.json"] = CanonicalJson.Serialize(Fixture.Set(fixture.Example("current"), "manifest", manifestRef));
            using var client = fixture.Client(transport, new FileConfigurationStore(directory.Path), new TestClock());
            var result = await client.GetFeatureAsync("progression");
            Assert.Equal("bounds", result.Reason);
            Assert.False(result.Durable);
            Assert.DoesNotContain(transport.Requests, path => path.StartsWith("assets/", StringComparison.Ordinal));
        }
        [Fact]
        public async Task Long_bounded_retry_after_defers_both_channels_without_in_wave_early_retry()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var clock = new TestClock();
            var transport = new FixtureTransport(fixture);
            transport.Override = (request, _) => Task.FromResult(new ConfigurationHttpResponse(429, Array.Empty<byte>(), request.Address, retryAfterSeconds: 9000));
            using var client = fixture.Client(transport, new FileConfigurationStore(directory.Path), clock);
            await client.TickAsync(true);
            Assert.Equal(2, transport.Requests.Count);
            clock.Advance(899);
            await client.TickAsync(true);
            Assert.Equal(2, transport.Requests.Count);
            clock.Advance(1);
            await client.TickAsync(true);
            Assert.Equal(4, transport.Requests.Count);
        }
        [Fact]
        public async Task Blocked_old_graph_keeps_captured_release_during_concurrent_refresh_and_restores()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var transport = new FixtureTransport(fixture);
            var clock = new TestClock();
            var store = new FileConfigurationStore(directory.Path);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            string b = fixture.Example("manifest")["assets"].AsArray().Single(a => a["revision"].AsString() == "B-1")["path"].AsString();
            transport.Override = async (request, token) => { string path = request.Address.AbsolutePath.TrimStart('/'); if (path == b) { entered.TrySetResult(true); await release.Task.WaitAsync(token); } return new ConfigurationHttpResponse(200, fixture.Documents[path], request.Address); };
            using var client = fixture.Client(transport, store, clock);
            var pending = client.GetFeatureAsync("progression");
            await entered.Task;
            fixture.AdvanceRelease();
            Assert.Equal("fresh", (await client.RefreshAsync()).Reason);
            release.SetResult(true);
            var old = await pending;
            Assert.True(old.Durable);
            Assert.Equal("B", old.Feature!.Selection.Choice.Variant);
            Assert.Equal(7, old.Feature.Origin.ReleaseSequence);
            Assert.Equal(fixture.Example("current")["manifest"]["representationSha"].AsString(), old.Feature.Origin.ManifestRepresentationSha);
            await client.PinAsync(old.Feature, Pin);
            using var restarted = fixture.Client(new FixtureTransport(fixture), store, clock);
            Assert.Equal("restored", (await restarted.RestoreAsync()).Reason);
            Assert.Equal("B", (await restarted.GetPinnedAsync(Pin)).Feature!.Selection.Choice.Variant);
            var next = await client.GetFeatureAsync("progression");
            Assert.Equal("A", next.Feature!.Selection.Choice.Variant);
            Assert.Equal(8, next.Feature.Origin.ReleaseSequence);
        }
        [Theory]
        [InlineData("configuration.newInstance")]
        [InlineData("configuration.newEnrollment")]
        public async Task Higher_admission_deny_during_download_rejects_new_context_and_preserves_prior_pin(string purpose)
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var clock = new TestClock();
            var store = new FileConfigurationStore(directory.Path);
            using var prior = fixture.Client(new FixtureTransport(fixture), store, clock);
            var existing = (await prior.GetFeatureAsync("progression")).Feature!;
            await prior.PinAsync(existing, Pin);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var transport = new FixtureTransport(fixture);
            string b = fixture.Example("manifest")["assets"].AsArray().Single(a => a["revision"].AsString() == "B-1")["path"].AsString();
            transport.Override = async (request, token) => { string path = request.Address.AbsolutePath.TrimStart('/'); if (path == b) { entered.TrySetResult(true); await release.Task.WaitAsync(token); } return new ConfigurationHttpResponse(200, fixture.Documents[path], request.Address); };
            using var newcomer = fixture.Client(transport, store, clock, new ConfigurationOwner("other-owner", fixture.Owner.SubjectId, 2));
            var pending = newcomer.GetFeatureAsync("progression");
            await entered.Task;
            fixture.AdmissionControl(12, purpose);
            Assert.True((await prior.RefreshControlsAsync()).Durable);
            release.SetResult(true);
            var rejected = await pending;
            Assert.Equal("bundled", rejected.Status);
            Assert.False(rejected.Durable);
            Assert.Null(rejected.Feature);
            Assert.Equal("B", (await prior.GetPinnedAsync(Pin)).Feature!.Selection.Choice.Variant);
            var snapshot = await store.LoadAsync(new CacheNamespace(fixture.Scope, new ConfigurationOwner("other-owner", fixture.Owner.SubjectId, 2)), CancellationToken.None);
            Assert.NotNull(snapshot);
            Assert.Empty(CanonicalJson.Parse(snapshot!.Metadata)["activations"].AsArray());
        }
        [Fact]
        public async Task Actual_loopback_gzip_file_store_cold_warm_restart_selects_B_shared_only()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            await using var server = new LocalHttpServer(fixture) { Gzip = true };
            using var transport = new LocalHttpTransport();
            var clock = new TestClock();
            var store = new FileConfigurationStore(directory.Path);
            using (var client = fixture.Client(transport, store, clock, root: server.Root))
            {
                var cold = await client.GetFeatureAsync("progression");
                Assert.Equal("available", cold.Status);
                Assert.True(cold.Durable);
                Assert.Equal("B", cold.Feature!.Selection.Choice.Variant);
                Assert.Equal(2, cold.Feature.Closure.Count);
                Assert.True((await client.PinAsync(cold.Feature, Pin)).Durable);
                string eventSha = CanonicalJson.SemanticSha256(cold.Feature.FreezeEventContext());
                var warm = await client.GetFeatureAsync("progression");
                Assert.Equal(eventSha, CanonicalJson.SemanticSha256(warm.Feature!.FreezeEventContext()));
                Assert.DoesNotContain(server.Requests, p => p.Contains("Store", StringComparison.Ordinal) || p.Contains("Packs", StringComparison.Ordinal));
                string unusedA = fixture.Example("manifest")["assets"].AsArray().Single(a => a["revision"].AsString() == "A-1")["path"].AsString();
                Assert.DoesNotContain(unusedA, server.Requests);
            }
            int requests = server.Requests.Count;
            using var restarted = fixture.Client(transport, new FileConfigurationStore(directory.Path), clock, root: server.Root);
            Assert.Equal("restored", (await restarted.RestoreAsync()).Reason);
            var pin = await restarted.GetPinnedAsync(Pin);
            Assert.Equal("B", pin.Feature!.Selection.Choice.Variant);
            Assert.Equal(requests, server.Requests.Count);
            Assert.Equal("available", (await restarted.GetFeatureAsync("progression")).Status);
            Assert.Equal(requests, server.Requests.Count);
        }
        [Fact]
        public async Task New_release_changes_next_context_and_preserves_pinned_old_context()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var transport = new FixtureTransport(fixture);
            var clock = new TestClock();
            using var client = fixture.Client(transport, new FileConfigurationStore(directory.Path), clock);
            var old = (await client.GetFeatureAsync("progression")).Feature!;
            await client.PinAsync(old, Pin);
            string frozen = CanonicalJson.SemanticSha256(old.FreezeEventContext());
            fixture.AdvanceRelease();
            await client.RefreshAsync();
            var next = await client.GetFeatureAsync("progression");
            Assert.Equal("A", next.Feature!.Selection.Choice.Variant);
            Assert.Equal(8, next.Feature.Origin.ReleaseSequence);
            var pinned = await client.GetPinnedAsync(Pin);
            Assert.Equal("B", pinned.Feature!.Selection.Choice.Variant);
            Assert.Equal(7, pinned.Feature.Origin.ReleaseSequence);
            Assert.Equal(frozen, CanonicalJson.SemanticSha256(pinned.Feature.FreezeEventContext()));
        }
        [Fact]
        public async Task Failed_durable_feature_write_preserves_old_activation_and_new_bytes_are_not_applied()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var faults = new FileFaults();
            var clock = new TestClock();
            using var client = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path, faults), clock);
            var old = await client.GetFeatureAsync("progression");
            fixture.AdvanceRelease();
            await client.RefreshAsync();
            faults.Remaining = 1;
            var failed = await client.GetFeatureAsync("progression");
            Assert.Equal("preserved", failed.Status);
            Assert.Equal("storage_failure", failed.Reason);
            Assert.Equal("B", failed.Feature!.Selection.Choice.Variant);
            Assert.True(failed.Durable);
            using var restarted = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path), clock);
            await restarted.RestoreAsync();
            var restored = await restarted.GetPinnedAsync(Pin);
            Assert.Equal("rejected", restored.Status);
            var next = await restarted.GetFeatureAsync("progression");
            Assert.Equal("A", next.Feature!.Selection.Choice.Variant);
        }
        [Fact]
        public async Task Lost_commit_ack_is_reconciled_to_real_durable_result()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var faults = new FileFaults { Boundary = "after_commit", Remaining = 1 };
            using var client = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path, faults), new TestClock());
            Assert.True((await client.GetFeatureAsync("progression")).Durable);
        }
        [Fact]
        public async Task Independent_control_denial_survives_normal_error_owner_switch_and_retirement()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var clock = new TestClock();
            var transport = new FixtureTransport(fixture);
            using var old = fixture.Client(transport, store, clock);
            var feature = (await old.GetFeatureAsync("progression")).Feature!;
            await old.PinAsync(feature, Pin);
            await old.RefreshControlsAsync();
            Assert.True(old.Restrictions.Denies("analytics.send"));
            transport.Override = (request, _) => Task.FromResult(new ConfigurationHttpResponse(request.Address.AbsolutePath.EndsWith("control.json", StringComparison.Ordinal) ? 200 : 503, request.Address.AbsolutePath.EndsWith("control.json", StringComparison.Ordinal) ? fixture.Documents["control.json"] : Array.Empty<byte>(), request.Address));
            var failedNormal = old.RefreshAsync();
            await clock.ShortDelayEntered.Task;
            clock.Advance(4);
            Assert.Equal("preserved", (await failedNormal).Status);
            old.Retire();
            Assert.True((await old.RefreshControlsAsync()).Durable);
            using var other = fixture.Client(new FixtureTransport(fixture), store, clock, new ConfigurationOwner("other", "other-subject", 2));
            await other.RestoreAsync();
            Assert.True(other.Restrictions.Denies("analytics.send"));
            Assert.Equal("rejected", (await other.GetPinnedAsync(Pin)).Status);
            using var reopened = fixture.Client(new FixtureTransport(fixture), store, clock, new ConfigurationOwner(fixture.Owner.OwnerId, fixture.Owner.SubjectId, 3));
            await reopened.RestoreAsync();
            Assert.Equal("B", (await reopened.GetPinnedAsync(Pin)).Feature!.Selection.Choice.Variant);
        }
        [Fact]
        public async Task Failed_control_deny_applies_in_memory_and_failed_clear_never_enables()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var faults = new FileFaults();
            var clock = new TestClock();
            using var client = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path, faults), clock);
            await client.RefreshControlsAsync();
            fixture.Control(12, false);
            faults.Remaining = 1;
            Assert.False((await client.RefreshControlsAsync()).Durable);
            Assert.True(client.Restrictions.Denies("analytics.send"));
            Assert.False(client.Restrictions.Durable);
            Assert.True((await client.RefreshControlsAsync()).Durable);
            Assert.False(client.Restrictions.Denies("analytics.send"));
            fixture.Control(13, true);
            faults.Remaining = 1;
            Assert.False((await client.RefreshControlsAsync()).Durable);
            Assert.True(client.Restrictions.Denies("analytics.send"));
        }
        [Fact]
        public async Task Sequence_lower_and_conflict_do_not_clear_accepted_control()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            using var client = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path), new TestClock());
            await client.RefreshControlsAsync();
            fixture.Control(10, false);
            Assert.Equal("lower_sequence", (await client.RefreshControlsAsync()).Reason);
            fixture.Control(11, false);
            Assert.Equal("sequence_conflict", (await client.RefreshControlsAsync()).Reason);
            Assert.True(client.Restrictions.Denies("analytics.send"));
        }
        [Fact]
        public async Task Stale_clock_rollback_and_ephemeral_identity_do_not_create_new_experiment_contexts()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var clock = new TestClock();
            using var client = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path), clock);
            var selected = (await client.GetFeatureAsync("progression")).Feature!;
            await client.PinAsync(selected, Pin);
            await client.RefreshControlsAsync();
            clock.Advance(86401);
            Assert.Equal("stale", (await client.GetFeatureAsync("progression")).Reason);
            Assert.Equal("B", (await client.GetPinnedAsync(Pin)).Feature!.Selection.Choice.Variant);
            Assert.False(client.AllowsOptionalAnalytics("analytics.collect", true, true, true));
            clock.UtcSeconds = 1791971990;
            Assert.Equal("unknown_time", (await client.GetFeatureAsync("progression")).Reason);
            using var ephemeral = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path), new TestClock(), new ConfigurationOwner("ephemeral", "ephemeral-subject", 1, false));
            var fallback = await ephemeral.GetFeatureAsync("progression");
            Assert.Equal("ephemeral_identity", fallback.Reason);
            Assert.False(fallback.Durable);
            Assert.NotNull(fallback.BundledChoice);
        }
        [Fact]
        public async Task Metadata_missing_unchosen_A_rejects_before_fetching_selected_payload()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var transport = new FixtureTransport(fixture);
            var value = fixture.Example("manifest");
            value = Fixture.Set(value, "assets", JsonValue.Array(value["assets"].AsArray().Where(a => a["revision"].AsString() != "A-1")));
            value = Fixture.Set(value, "retained", JsonValue.Array(value["retained"].AsArray().Where(a => a["revision"].AsString() != "A-1")));
            byte[] manifest = CanonicalJson.Serialize(value);
            string sha = CanonicalJson.Sha256(manifest);
            var reference = Fixture.Object(("path", JsonValue.String("manifests/" + sha + ".json")), ("representationSha", JsonValue.String(sha)), ("semanticSha", JsonValue.String(sha)), ("decodedBytes", JsonValue.Integer(manifest.Length)));
            fixture.Documents[reference["path"].AsString()] = manifest;
            fixture.Documents["current.json"] = CanonicalJson.Serialize(Fixture.Set(fixture.Example("current"), "manifest", reference));
            using var client = fixture.Client(transport, new FileConfigurationStore(directory.Path), new TestClock());
            Assert.Equal("graph_invalid", (await client.GetFeatureAsync("progression")).Reason);
            Assert.DoesNotContain(transport.Requests, p => p.StartsWith("assets/", StringComparison.Ordinal));
        }
        [Fact]
        public async Task Corrupt_selected_payload_preserves_bundled_and_never_reports_durable()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            string b = fixture.Example("manifest")["assets"].AsArray().Single(a => a["revision"].AsString() == "B-1")["path"].AsString();
            fixture.Documents[b] = fixture.Documents[b].Concat(new byte[] { 32 }).ToArray();
            using var client = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path), new TestClock());
            var result = await client.GetFeatureAsync("progression");
            Assert.False(result.Durable);
            Assert.True(result.Reason == "bounds" || result.Reason == "integrity");
            Assert.Null(result.Feature);
        }
        [Fact]
        public async Task Concurrent_pointer_requests_share_one_flight_and_caller_cancel_does_not_activate()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var transport = new FixtureTransport(fixture);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            transport.Override = async (request, token) => { if (request.Address.AbsolutePath.EndsWith("current.json", StringComparison.Ordinal)) { entered.TrySetResult(true); await release.Task.WaitAsync(token); } return new ConfigurationHttpResponse(200, fixture.Documents[request.Address.AbsolutePath.TrimStart('/')], request.Address); };
            using var client = fixture.Client(transport, new FileConfigurationStore(directory.Path), new TestClock());
            using var cancelled = new CancellationTokenSource();
            var first = client.RefreshAsync(cancelled.Token);
            await entered.Task;
            var second = client.RefreshAsync();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            release.SetResult(true);
            Assert.Equal("metadata_only", (await second).Status);
            Assert.Equal(1, transport.Requests.Count(p => p == "current.json"));
        }
        [Fact]
        public async Task Retired_owner_rejects_late_pointer_before_durable_activation()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var transport = new FixtureTransport(fixture);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            transport.Override = async (request, token) => { entered.TrySetResult(true); await release.Task; return new ConfigurationHttpResponse(200, fixture.Documents[request.Address.AbsolutePath.TrimStart('/')], request.Address); };
            var store = new FileConfigurationStore(directory.Path);
            using var client = fixture.Client(transport, store, new TestClock());
            var pending = client.RefreshAsync();
            await entered.Task;
            client.Retire();
            release.SetResult(true);
            Assert.Equal("owner_changed", (await pending).Reason);
            Assert.Null(await store.LoadAsync(new CacheNamespace(fixture.Scope, fixture.Owner), CancellationToken.None));
        }
        [Fact]
        public async Task Invalid_304_without_original_raw_object_gets_one_unconditional_retry()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var space = new CacheNamespace(fixture.Scope, fixture.Owner);
            await store.CommitAsync(space, 0, fixture.Metadata("cache", 1), fixture.Objects("cache"), CancellationToken.None);
            string sha = fixture.Example("cache")["normal"]["pointerRepresentationSha"].AsString();
            File.Delete(Path.Combine(directory.Path, space.Key, "objects", sha + ".json"));
            var transport = new FixtureTransport(fixture);
            int calls = 0;
            transport.Override = (request, _) => { string path = request.Address.AbsolutePath.TrimStart('/'); if (path == "current.json" && calls++ == 0) return Task.FromResult(new ConfigurationHttpResponse(304, Array.Empty<byte>(), request.Address)); return Task.FromResult(new ConfigurationHttpResponse(200, fixture.Documents[path], request.Address)); };
            using var client = fixture.Client(transport, store, new TestClock());
            Assert.Equal("metadata_only", (await client.RefreshAsync()).Status);
            Assert.Equal(2, calls);
            Assert.Contains(client.Diagnostics, d => d.Reason == "no_cached_validator");
        }
        [Fact]
        public async Task Two_real_store_clients_merge_distinct_pins_with_cas_and_release_explicitly()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var clock = new TestClock();
            using var a = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path), clock);
            var graph = (await a.GetFeatureAsync("progression")).Feature!;
            using var b = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path), clock);
            await b.RestoreAsync();
            var graphB = (await b.GetFeatureAsync("progression")).Feature!;
            string secondPin = "0199e584-6400-7000-8000-000000000002";
            await a.PinAsync(graph, Pin);
            await b.PinAsync(graphB, secondPin);
            using var reopened = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path), clock);
            await reopened.RestoreAsync();
            Assert.True((await reopened.GetPinnedAsync(Pin)).Durable);
            Assert.True((await reopened.GetPinnedAsync(secondPin)).Durable);
            Assert.True(await reopened.ReleasePinAsync(Pin));
            Assert.Equal("rejected", (await reopened.GetPinnedAsync(Pin)).Status);
        }
        [Fact]
        public async Task Cross_owner_and_stale_generation_handles_cannot_be_pinned_but_restore_mints_current_handles()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var store = new FileConfigurationStore(directory.Path);
            var clock = new TestClock();
            using var old = fixture.Client(new FixtureTransport(fixture), store, clock);
            var context = (await old.GetFeatureAsync("progression")).Feature!;
            await old.PinAsync(context, Pin);
            using var other = fixture.Client(new FixtureTransport(fixture), store, clock, new ConfigurationOwner("other", "other-subject", 2));
            Assert.Equal("owner_changed", (await other.PinAsync(context, Pin)).Reason);
            using var current = fixture.Client(new FixtureTransport(fixture), store, clock, new ConfigurationOwner(fixture.Owner.OwnerId, fixture.Owner.SubjectId, 2));
            Assert.Equal("owner_changed", (await current.PinAsync(context, Pin)).Reason);
            await current.RestoreAsync();
            var restored = (await current.GetPinnedAsync(Pin)).Feature!;
            string second = "0199e584-6400-7000-8000-000000000002";
            Assert.True((await current.PinAsync(restored, second)).Durable);
            Assert.Equal(CanonicalJson.SemanticSha256(context.FreezeEventContext()), CanonicalJson.SemanticSha256(restored.FreezeEventContext()));
        }
        [Fact]
        public async Task Analytics_permissions_close_unknown_keys_and_validate_all_rate_bounds()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            using var client = fixture.Client(new FixtureTransport(fixture), new FileConfigurationStore(directory.Path), new TestClock());
            await client.RefreshAsync();
            fixture.Control(12, false);
            await client.RefreshControlsAsync();
            Assert.True(client.AllowsOptionalAnalytics("analytics.send", true, true, true));
            Assert.False(client.AllowsOptionalAnalytics("analytics.unknown", true, true, true));
            Assert.False(client.AllowsOptionalAnalytics("configuration.newEnrollment", true, true, true));
            Assert.Equal(100, client.AnalyticsSamplePermille(700, 400, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => client.AnalyticsSamplePermille(1001, 400, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => client.Restrictions.CapPermille("analytics.sample", -1));
            Assert.Throws<ArgumentException>(() => client.Restrictions.CapPermille("unknown", 100));
        }
        [Fact]
        public async Task Injected_host_clock_expires_entire_wave_and_cancels_real_transport_wait()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var clock = new TestClock();
            var transport = new FixtureTransport(fixture);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool cancelled = false;
            transport.Override = async (request, token) => { entered.TrySetResult(true); try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { cancelled = true; throw; } return new ConfigurationHttpResponse(200, Array.Empty<byte>(), request.Address); };
            using var client = fixture.Client(transport, new FileConfigurationStore(directory.Path), clock);
            var refresh = client.RefreshAsync();
            await entered.Task;
            clock.Advance(20);
            var result = await refresh;
            Assert.Equal("cancelled", result.Reason);
            Assert.True(cancelled);
            Assert.False(result.Durable);
        }
        [Fact]
        public async Task Graph_downloads_use_remaining_wave_budget_after_metadata()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var clock = new TestClock();
            var transport = new FixtureTransport(fixture);
            var graphEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            transport.Override = async (request, token) => { string path = request.Address.AbsolutePath.TrimStart('/'); if (path.StartsWith("selectors/", StringComparison.Ordinal)) clock.Advance(16); if (path.StartsWith("assets/", StringComparison.Ordinal)) { graphEntered.TrySetResult(true); await Task.Delay(Timeout.Infinite, token); } return new ConfigurationHttpResponse(200, fixture.Documents[path], request.Address); };
            using var client = fixture.Client(transport, new FileConfigurationStore(directory.Path), clock);
            var feature = client.GetFeatureAsync("progression");
            await graphEntered.Task;
            clock.Advance(4);
            var result = await feature;
            Assert.Equal("cancelled", result.Reason);
            Assert.False(result.Durable);
            Assert.Null(result.Feature);
        }
        [Fact]
        public async Task Volatile_failed_deny_and_failed_clear_survive_owner_subject_replacement()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var faults = new FileFaults();
            var store = new FileConfigurationStore(directory.Path, faults);
            var clock = new TestClock();
            fixture.Control(12, false);
            using var old = fixture.Client(new FixtureTransport(fixture), store, clock);
            await old.RefreshControlsAsync();
            fixture.Control(13, true);
            faults.Remaining = 1;
            Assert.False((await old.RefreshControlsAsync()).Durable);
            Assert.True(old.Restrictions.Denies("analytics.send"));
            using var replacement = fixture.Client(new FixtureTransport(fixture), store, clock, new ConfigurationOwner("replacement-owner", "replacement-subject", 2));
            await replacement.RestoreAsync();
            Assert.Equal(13, replacement.Restrictions.Sequence);
            Assert.True(replacement.Restrictions.Denies("analytics.send"));
            Assert.False(replacement.Restrictions.Durable);
            old.Dispose();
            Assert.True(replacement.Restrictions.Denies("analytics.send"));
            Assert.True((await replacement.RefreshControlsAsync()).Durable);
            Assert.True(replacement.Restrictions.Denies("analytics.send"));
            fixture.Control(14, false);
            faults.Remaining = 1;
            Assert.False((await replacement.RefreshControlsAsync()).Durable);
            using var third = fixture.Client(new FixtureTransport(fixture), store, clock, new ConfigurationOwner("third-owner", "third-subject", 3));
            await third.RestoreAsync();
            Assert.True(third.Restrictions.Denies("analytics.send"));
            Assert.True(replacement.Restrictions.Denies("analytics.send"));
            Assert.True((await third.RefreshControlsAsync()).Durable);
            Assert.False(third.Restrictions.Denies("analytics.send"));
            Assert.False(replacement.Restrictions.Denies("analytics.send"));
        }
        [Fact]
        public async Task Shared_control_memory_serializes_requests_and_replay_keeps_durable_floor()
        {
            var fixture = new Fixture();
            using var directory = new OwnedDirectory();
            var clock = new TestClock();
            var transport = new FixtureTransport(fixture);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            transport.Override = async (request, token) => { entered.TrySetResult(true); await release.Task.WaitAsync(token); return new ConfigurationHttpResponse(200, fixture.Documents["control.json"], request.Address); };
            using var a = fixture.Client(transport, new FileConfigurationStore(directory.Path), clock);
            using var b = fixture.Client(transport, new FileConfigurationStore(directory.Path), clock, new ConfigurationOwner("other", "other-subject", 2));
            var first = a.RefreshControlsAsync();
            await entered.Task;
            var second = b.RefreshControlsAsync();
            Assert.Single(transport.Requests);
            release.SetResult(true);
            Assert.True((await first).Durable);
            Assert.True((await second).Durable);
            Assert.Equal(11, a.Restrictions.Sequence);
            Assert.Equal(11, b.Restrictions.Sequence);
            Assert.True(a.Restrictions.Denies("analytics.send"));
        }
        [Fact]
        public void Shared_control_memory_rejects_scope_mismatch()
        {
            var fixture = new Fixture();
            var clock = new TestClock();
            using var directory = new OwnedDirectory();
            Assert.Throws<InvalidConfigurationException>(() => new ConfigurationClient(fixture.Scope, fixture.Owner, new ConfigurationClientPolicy(new Uri("http://127.0.0.1:1/"), new Uri("http://127.0.0.1:1/"), true), new FixtureTransport(fixture), new FileConfigurationStore(directory.Path), clock, clock, fixture.Bundled, new ConfigurationControlMemory(new ConfigurationScope("wrong", "local"))));
        }
    }
}
