using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Configuration.Client
{
    /// <summary>Optional host-driven lifecycle. Constructors start no IO and evaluation stays pure.</summary>
    public sealed class ConfigurationClient : IDisposable
    {
        private sealed class ReleaseView
        {
            internal readonly ReleasePointer Pointer;
            internal readonly ReleaseManifest Manifest;
            internal readonly CompiledConfiguration Selector;
            internal readonly JsonValue Observation;
            internal readonly long? ObservedMono;
            internal readonly IReadOnlyDictionary<string, byte[]> Metadata;
            internal ReleaseView(ReleasePointer pointer, ReleaseManifest manifest, CompiledConfiguration selector, JsonValue observation, long? observedMono, IReadOnlyDictionary<string, byte[]> metadata)
            {
                Pointer = pointer;
                Manifest = manifest;
                Selector = selector;
                Observation = observation;
                ObservedMono = observedMono;
                Metadata = metadata.ToDictionary(p => p.Key, p => (byte[])p.Value.Clone(), StringComparer.Ordinal);
            }
        }
        private sealed class RetryAllowance
        {
            private int used; internal bool Take() => Interlocked.CompareExchange(ref used, 1, 0) == 0;
        }
        private sealed class NormalWave
        {
            internal readonly long Deadline;
            internal RetryAllowance Retries = new RetryAllowance();
            internal NormalWave(long deadline)
            {
                Deadline = deadline;
            }
        }
        private readonly ConfigurationScope scope;
        private readonly ConfigurationOwner owner;
        private readonly ConfigurationClientPolicy policy;
        private readonly IConfigurationTransport transport;
        private readonly IConfigurationStore store;
        private readonly IConfigurationClock clock;
        private readonly IConfigurationRandom random;
        private readonly CompiledConfiguration bundled;
        private readonly ConfigurationControlMemory controlMemory;
        private readonly CacheNamespace space;
        private readonly CacheNamespace publicSpace;
        private readonly CancellationTokenSource normalLifetime = new CancellationTokenSource();
        private readonly CancellationTokenSource publicLifetime = new CancellationTokenSource();
        private readonly SemaphoreSlim featureGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim assetGate = new SemaphoreSlim(2, 2);
        private readonly object sync = new object();
        private readonly Dictionary<string, Task<byte[]>> flights = new Dictionary<string, Task<byte[]>>(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, FeatureContext> features = new Dictionary<string, FeatureContext>(StringComparer.Ordinal);
        private readonly Dictionary<string, FeatureContext> pins = new Dictionary<string, FeatureContext>(StringComparer.Ordinal);
        private readonly Queue<ConfigurationDiagnostic> diagnostics = new Queue<ConfigurationDiagnostic>();
        private CacheState state = new CacheState();
        private CacheState publicState = new CacheState();
        private ReleaseView? releaseView;
        private Task<ConfigurationResult>? normalFlight;
        private NormalWave? normalWave;
        private Task<ConfigurationResult>? controlFlight;
        private long? normalObservedMono;
        private long? controlObservedMono;
        private long nextNormal;
        private long nextControl;
        private long retryNormalAt;
        private long retryControlAt;
        private long lastNormalAttempt = long.MinValue;
        private int normalFailures;
        private int controlFailures;
        private bool retired;
        private bool disposed;
        public ConfigurationClient(ConfigurationScope scope, ConfigurationOwner owner, ConfigurationClientPolicy policy, IConfigurationTransport transport, IConfigurationStore store, IConfigurationClock clock, IConfigurationRandom random, CompiledConfiguration bundled, ConfigurationControlMemory controlMemory)
        {
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.bundled = bundled ?? throw new ArgumentNullException(nameof(bundled));
            this.controlMemory = controlMemory ?? throw new ArgumentNullException(nameof(controlMemory));
            if (controlMemory.Scope.App != scope.App || controlMemory.Scope.Environment != scope.Environment)
                throw new InvalidConfigurationException("scope_mismatch");
            if (bundled.Definition.App != scope.App || bundled.Definition.Environment != scope.Environment)
                throw new InvalidConfigurationException("scope_mismatch");
            space = new CacheNamespace(scope, owner);
            publicSpace = new CacheNamespace(scope, null);
        }
        public OperationalRestrictions Restrictions => controlMemory.Current;
        public IReadOnlyList<ConfigurationDiagnostic> Diagnostics
        {
            get
            {
                lock (sync)
                    return diagnostics.ToArray();
            }
        }
        private void Record(string channel, string reason)
        {
            lock (sync)
            {
                if (diagnostics.Count == 32)
                    diagnostics.Dequeue();
                diagnostics.Enqueue(new ConfigurationDiagnostic(channel, reason, Math.Max(0, clock.UtcSeconds)));
            }
        }
        private void Alive(bool normal)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ConfigurationClient));
            if (normal && retired)
                throw new InvalidConfigurationException("owner_changed");
        }
        public void Retire()
        {
            lock (sync)
            {
                retired = true;
                normalLifetime.Cancel();
            }
        }
        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                retired = true;
                normalLifetime.Cancel();
                publicLifetime.Cancel();
            }
        }
        private static async Task<T> Wait<T>(Task<T> task, CancellationToken token)
        {
            if (!token.CanBeCanceled)
                return await task.ConfigureAwait(true);
            var cancellation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = token.Register(() => cancellation.TrySetResult(true));
            if (await Task.WhenAny(task, cancellation.Task).ConfigureAwait(true) != task)
                token.ThrowIfCancellationRequested();
            return await task.ConfigureAwait(true);
        }
        private static string Reason(Exception error)
        {
            if (error is OperationCanceledException)
                return "cancelled";
            if (!(error is InvalidConfigurationException))
                return "unavailable";
            string code = error.Message;
            if (code == "scope_mismatch" || code == "owner_changed" || code == "lower_sequence" || code == "sequence_conflict" || code == "protected_capacity" || code == "storage_failure" || code == "quota" || code == "no_cached_validator" || code == "unknown_time" || code == "stale")
                return code;
            if (code.Contains("bounds") || code.Contains("limit"))
                return "bounds";
            if (code.Contains("integrity") || code.Contains("digest"))
                return "integrity";
            if (code.Contains("graph") || code.Contains("selection") || code.Contains("closure"))
                return "graph_invalid";
            return "schema_invalid";
        }
        private ConfigurationResult Fallback(string key, string reason)
        {
            if (retired || disposed || reason == "owner_changed")
                return Default(key, "owner_changed");
            if (Restrictions.Denies("configuration.newInstance"))
                return Default(key, "unavailable");
            if (features.TryGetValue(key, out var previous))
            {
                if (AdmissionDenied(previous.Selection))
                    return Default(key, "unavailable");
                if (previous.Selection.Experiment != null && !Fresh(state.Normal, normalObservedMono))
                    return Default(key, FreshnessReason(state.Normal, normalObservedMono));
                return new ConfigurationResult("preserved", reason, previous.Durable, previous);
            }
            foreach (var feature in bundled.Definition.Features)
                if (feature.Key == key)
                    return new ConfigurationResult("bundled", reason, false, bundledChoice: feature.Default);
            return new ConfigurationResult("rejected", "missing_feature", false);
        }
        private ConfigurationResult Default(string key, string reason)
        {
            foreach (var feature in bundled.Definition.Features)
                if (feature.Key == key)
                    return new ConfigurationResult("bundled", reason, false, bundledChoice: feature.Default);
            return new ConfigurationResult("rejected", "missing_feature", false);
        }
        private JsonValue Time() => Wire.Object(("observedAtSeconds", JsonValue.Integer(clock.UtcSeconds)), ("anchorAtSeconds", JsonValue.Integer(clock.UtcSeconds)), ("status", JsonValue.String(clock.IsTrusted ? "trusted" : "unknown")));
        private bool Fresh(JsonValue? observation, long? observedMono)
        {
            if (observation == null || !clock.IsTrusted || observation["time"]["status"].AsString() != "trusted")
                return false;
            long anchor = observation["time"]["anchorAtSeconds"].AsInteger(), wall = clock.UtcSeconds - anchor;
            if (wall < 0 || clock.UtcSeconds > ConfigurationLimits.SafeInteger)
                return false;
            if (observedMono.HasValue)
            {
                long elapsed = clock.MonotonicSeconds - observedMono.Value;
                if (elapsed < 0 || Math.Abs(wall - elapsed) > 300)
                    return false;
                return elapsed <= ConfigurationClientPolicy.FreshSeconds;
            }
            // Across restart IsTrusted is the host's independently verified time-anchor assertion.
            return wall <= ConfigurationClientPolicy.FreshSeconds;
        }
        private string FreshnessReason(JsonValue? observation, long? mono)
        {
            if (!clock.IsTrusted || observation == null || observation["time"]["status"].AsString() != "trusted")
                return "unknown_time";
            long delta = clock.UtcSeconds - observation["time"]["anchorAtSeconds"].AsInteger();
            if (delta < 0 || (mono.HasValue && (clock.MonotonicSeconds < mono.Value || Math.Abs(delta - (clock.MonotonicSeconds - mono.Value)) > 300)))
                return "unknown_time";
            return "stale";
        }
        public bool AllowsOptionalAnalytics(string key, bool consent, bool builtInAllows, bool normalAllows)
        {
            return (key == "analytics.collect" || key == "analytics.send") && consent && builtInAllows && normalAllows && Fresh(state.Normal, normalObservedMono) && ControlFresh() && !Restrictions.Denies(key);
        }
        private bool ControlFresh() => publicState.Control != null && Fresh(publicState.Control, controlObservedMono) && ProtocolReader.Sequence(publicState.Control["document"]["sequence"], true) == Restrictions.Sequence && publicState.Control["representationSha"].AsString() == Restrictions.RepresentationSha && Restrictions.Durable;
        public int AnalyticsSamplePermille(int builtInRate, int normalRate, int consentRate)
        {
            if (builtInRate < 0 || builtInRate > 1000 || normalRate < 0 || normalRate > 1000 || consentRate < 0 || consentRate > 1000)
                throw new ArgumentOutOfRangeException(nameof(builtInRate));
            if (!Fresh(state.Normal, normalObservedMono) || !ControlFresh() || Restrictions.Denies("analytics.collect") || Restrictions.Denies("analytics.send"))
                return 0;
            return Restrictions.CapPermille("analytics.sample", Math.Min(builtInRate, Math.Min(normalRate, consentRate)));
        }
        private async Task<CacheState> Load(CacheNamespace target, CancellationToken token)
        {
            var snapshot = await store.LoadAsync(target, token).ConfigureAwait(true);
            if (snapshot == null)
                return new CacheState();
            var loaded = ProtocolReader.Cache(snapshot.Metadata, target);
            if (loaded.Generation != snapshot.Generation)
                throw new InvalidConfigurationException("store_generation");
            if (target.Owner != null && loaded.OwnerGeneration > target.Owner.Generation)
                throw new InvalidConfigurationException("owner_changed");
            return loaded;
        }
        private async Task<byte[]> Stored(CacheNamespace target, string sha, int maximum, CancellationToken token)
        {
            byte[]? result = await store.ReadObjectAsync(target, sha, maximum, token).ConfigureAwait(true);
            if (result == null || result.Length > maximum || CanonicalJson.Sha256(result) != sha)
                throw new InvalidConfigurationException("stored_integrity");
            return result;
        }
        private async Task ValidateObservation(CacheNamespace target, JsonValue observation, bool control, CancellationToken token)
        {
            string sha = observation[control ? "representationSha" : "pointerRepresentationSha"].AsString();
            byte[] raw = await Stored(target, sha, control ? ConfigurationClientPolicy.ControlBytes : ConfigurationLimits.Bytes, token).ConfigureAwait(true);
            var document = control ? ProtocolReader.Control(raw, scope).Document : ProtocolReader.Pointer(raw, scope).Document;
            if (CanonicalJson.SemanticSha256(document) != CanonicalJson.SemanticSha256(observation[control ? "document" : "pointer"]))
                throw new InvalidConfigurationException("stored_integrity");
        }
        private async Task<FeatureContext> RestoreActivation(JsonValue activation, CancellationToken token)
        {
            string key = activation["selection"]["featureKey"].AsString();
            var origin = ProtocolReader.Origin(activation["origin"]);
            var selection = ProtocolReader.ReadSelection(activation["selection"]);
            byte[] manifestBytes = await Stored(space, origin.ManifestRepresentationSha, ConfigurationLimits.Bytes, token).ConfigureAwait(true);
            var retainedManifest = ProtocolReader.Manifest(manifestBytes, scope);
            byte[] selectorBytes = await Stored(space, origin.SelectorRepresentationSha, ConfigurationLimits.Bytes, token).ConfigureAwait(true);
            FeatureGraph.VerifyFile(selectorBytes, retainedManifest.Selector, true);
            var retainedSelector = ConfigurationReader.Read(selectorBytes, scope.App, scope.Environment);
            FeatureGraph.ValidateMetadata(retainedManifest, retainedSelector);
            FeatureGraph.ValidateSelection(retainedSelector, key, selection, owner.SubjectId);
            var graph = FeatureGraph.Chosen(retainedManifest, key, selection.Choice);
            var closure = graph.Select(a => a.Reference).ToArray();
            if (CanonicalJson.SemanticSha256(Wire.References(closure)) != CanonicalJson.SemanticSha256(activation["closure"]))
                throw new InvalidConfigurationException("closure_mismatch");
            JsonValue? payload = null;
            foreach (var asset in graph)
            {
                byte[] content = await Stored(space, asset.File.RepresentationSha, asset.File.DecodedBytes, token).ConfigureAwait(true);
                var item = FeatureGraph.Asset(content, scope, asset);
                if (asset.Reference.Key == key && asset.Reference.Revision == selection.Choice.Revision)
                    payload = item;
            }
            return new FeatureContext(space, key, selection, origin, closure, payload ?? throw new InvalidConfigurationException("graph_missing"), true);
        }
        public async Task<ConfigurationResult> RestoreAsync(CancellationToken cancellationToken = default)
        {
            Alive(true);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, normalLifetime.Token);
            var token = linked.Token;
            await featureGate.WaitAsync(token).ConfigureAwait(true);
            try
            {
                publicState = await Load(publicSpace, token).ConfigureAwait(true);
                if (publicState.Control != null)
                {
                    try
                    {
                        await ValidateObservation(publicSpace, publicState.Control, true, token).ConfigureAwait(true);
                        var value = ProtocolReader.Control(CanonicalJson.Serialize(publicState.Control["document"]), scope);
                        controlMemory.Observe(new OperationalRestrictions(value.Sequence, publicState.Control["representationSha"].AsString(), true, new Dictionary<string, int>(value.Restrictions)), true);
                    }
                    catch (InvalidConfigurationException) { publicState.Control = null; Record("control", "integrity"); }
                }
                state = owner.Persistent ? await Load(space, token).ConfigureAwait(true) : new CacheState();
                features.Clear();
                pins.Clear();
                lock (sync)
                    releaseView = null;
                if (state.Normal != null)
                {
                    try
                    {
                        await ValidateObservation(space, state.Normal, false, token).ConfigureAwait(true);
                        var restoredPointer = ProtocolReader.Pointer(CanonicalJson.Serialize(state.Normal["pointer"]), scope);
                        byte[] manifestBytes = await Stored(space, restoredPointer.Manifest.RepresentationSha, restoredPointer.Manifest.DecodedBytes, token).ConfigureAwait(true);
                        FeatureGraph.VerifyFile(manifestBytes, restoredPointer.Manifest, true);
                        var restoredManifest = ProtocolReader.Manifest(manifestBytes, scope);
                        byte[] selectorBytes = await Stored(space, restoredManifest.Selector.RepresentationSha, restoredManifest.Selector.DecodedBytes, token).ConfigureAwait(true);
                        FeatureGraph.VerifyFile(selectorBytes, restoredManifest.Selector, true);
                        var restoredSelector = ConfigurationReader.Read(selectorBytes, scope.App, scope.Environment);
                        FeatureGraph.ValidateMetadata(restoredPointer, restoredManifest, restoredSelector);
                        string pointerSha = state.Normal["pointerRepresentationSha"].AsString();
                        byte[] pointerRaw = await Stored(space, pointerSha, ConfigurationLimits.Bytes, token).ConfigureAwait(true);
                        var retained = new Dictionary<string, byte[]> { [pointerSha] = pointerRaw, [restoredPointer.Manifest.RepresentationSha] = manifestBytes, [restoredManifest.Selector.RepresentationSha] = selectorBytes };
                        lock (sync)
                            releaseView = new ReleaseView(restoredPointer, restoredManifest, restoredSelector, state.Normal, null, retained);
                    }
                    catch (InvalidConfigurationException) { state.Normal = null; lock (sync) releaseView = null; Record("normal", "integrity"); }
                }
                foreach (var pair in state.Activations)
                {
                    try
                    {
                        features[pair.Key] = await RestoreActivation(pair.Value, token).ConfigureAwait(true);
                    }
                    catch (InvalidConfigurationException) { Record("restore", "graph_invalid"); }
                }
                foreach (var pair in state.Pins)
                {
                    try
                    {
                        pins[pair.Key] = await RestoreActivation(pair.Value["activation"], token).ConfigureAwait(true);
                    }
                    catch (InvalidConfigurationException) { Record("restore", "graph_invalid"); }
                }
                Alive(true);
                normalObservedMono = null;
                controlObservedMono = null;
                return new ConfigurationResult(features.Count > 0 ? "available" : "bundled", features.Count > 0 ? "restored" : "unavailable", features.Count > 0);
            }
            catch (Exception error) when (error is InvalidConfigurationException || error is System.IO.IOException || error is UnauthorizedAccessException) { Record("restore", Reason(error)); return new ConfigurationResult("bundled", Reason(error), false); }
            finally { featureGate.Release(); }
        }
        private async Task<ConfigurationHttpResponse> Fetch(string path, bool control, string? etag, bool immutable, int maximum, CancellationToken token, bool retry, NormalWave? wave = null)
        {
            long start = clock.MonotonicSeconds;
            wave = wave ?? new NormalWave(checked(start + 20));
            while (true)
            {
                using var timeout = new HostDeadline(clock, token, Math.Min(wave.Deadline, checked(clock.MonotonicSeconds + 5)));
                ConfigurationHttpResponse? response = null;
                try
                {
                    if (!control)
                        await assetGate.WaitAsync(timeout.Token).ConfigureAwait(true);
                    try
                    {
                        var request = new ConfigurationHttpRequest(policy.Resolve(path, control), maximum, etag, immutable);
                        response = await transport.FetchAsync(request, timeout.Token).ConfigureAwait(true);
                        policy.Response(request, response);
                    }
                    finally { if (!control) assetGate.Release(); }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                catch (System.IO.IOException) { }
                bool retryable = response == null || response.Status == 408 || response.Status == 429 || response.Status >= 500;
                int retryAfter = retryable && response?.RetryAfterSeconds.HasValue == true ? Math.Min(900, response.RetryAfterSeconds.Value) : 0;
                if (retryAfter > 0)
                {
                    long deferred = checked(clock.MonotonicSeconds + retryAfter);
                    lock (sync)
                    {
                        if (control)
                            retryControlAt = Math.Max(retryControlAt, deferred);
                        else
                            retryNormalAt = Math.Max(retryNormalAt, deferred);
                    }
                }
                if (!retry || !retryable || !wave.Retries.Take())
                {
                    if (response == null)
                        throw new System.IO.IOException("transport_unavailable");
                    return response;
                }
                double jitter = Random();
                int wait = Math.Max(retryAfter, 2 + (int)(jitter * 4));
                if (clock.MonotonicSeconds + wait + 5 > (wave.Deadline))
                    throw new System.IO.IOException("wave_exhausted");
                await clock.DelayAsync(TimeSpan.FromSeconds(wait), token).ConfigureAwait(true);
            }
        }
        private double Random()
        {
            double value = random.NextDouble();
            if (double.IsNaN(value) || value < 0 || value >= 1)
                throw new InvalidOperationException("random_out_of_range");
            return value;
        }
        private async Task<byte[]> Immutable(ImmutableFileReference reference, CancellationToken caller, NormalWave wave)
        {
            lock (sync)
                if (bytes.TryGetValue(reference.RepresentationSha, out var found))
                    return (byte[])found.Clone();
            Task<byte[]> flight;
            lock (sync)
            {
                if (!flights.TryGetValue(reference.RepresentationSha, out flight!))
                {
                    flight = DownloadBounded(reference, wave);
                    flights.Add(reference.RepresentationSha, flight);
                }
            }
            try
            {
                return await Wait(flight, caller).ConfigureAwait(true);
            }
            finally { if (flight.IsCompleted) lock (sync) flights.Remove(reference.RepresentationSha); }
        }
        private async Task<byte[]> DownloadBounded(ImmutableFileReference reference, NormalWave wave)
        {
            using var deadline = new HostDeadline(clock, normalLifetime.Token, wave.Deadline);
            return await Download(reference, deadline.Token, wave).ConfigureAwait(true);
        }
        private void Remember(string sha, byte[] content)
        {
            lock (sync)
            {
                long total = 0;
                foreach (var value in bytes.Values)
                    total += value.Length;
                foreach (string key in bytes.Keys.ToArray())
                {
                    if (total + content.Length <= ConfigurationClientPolicy.CacheBudgetBytes)
                        break;
                    total -= bytes[key].Length;
                    bytes.Remove(key);
                }
                bytes[sha] = content;
            }
        }
        private async Task<byte[]> Download(ImmutableFileReference reference, CancellationToken token, NormalWave wave)
        {
            if (owner.Persistent)
            {
                try
                {
                    byte[]? cached = await store.ReadObjectAsync(space, reference.RepresentationSha, reference.DecodedBytes, token).ConfigureAwait(true);
                    if (cached != null)
                    {
                        FeatureGraph.VerifyFile(cached, reference, false);
                        Remember(reference.RepresentationSha, cached);
                        return (byte[])cached.Clone();
                    }
                }
                catch (InvalidConfigurationException) { }
            }
            var response = await Fetch(reference.Path, false, null, true, reference.DecodedBytes, token, true, wave).ConfigureAwait(true);
            if (response.Status != 200)
                throw new System.IO.IOException("immutable_unavailable");
            byte[] content = response.Bytes;
            FeatureGraph.VerifyFile(content, reference, false);
            Remember(reference.RepresentationSha, content);
            return (byte[])content.Clone();
        }
        private static void Accept(long current, string currentDigest, long candidate, string candidateDigest)
        {
            if (candidate < current)
                throw new InvalidConfigurationException("lower_sequence");
            if (candidate == current && currentDigest != candidateDigest)
                throw new InvalidConfigurationException("sequence_conflict");
        }
        private async Task<HashSet<string>> Protected(CacheNamespace target, CacheState candidate, IReadOnlyDictionary<string, byte[]> added, CancellationToken token)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (candidate.Normal != null)
            {
                result.Add(candidate.Normal["pointerRepresentationSha"].AsString());
                result.Add(candidate.Normal["manifestRepresentationSha"].AsString());
                result.Add(candidate.Normal["selectorRepresentationSha"].AsString());
            }
            if (candidate.Control != null)
                result.Add(candidate.Control["representationSha"].AsString());
            // Pins commonly share an immutable manifest. Decode each unique representation once
            // per retention pass; every activation still validates its own complete closure below.
            var manifests = new Dictionary<string, ReleaseManifest>(StringComparer.Ordinal);
            var activations = candidate.Activations.Values.Concat(candidate.Pins.Values.Select(v => v["activation"]));
            foreach (var activation in activations)
            {
                var origin = ProtocolReader.Origin(activation["origin"]);
                result.Add(origin.ManifestRepresentationSha);
                result.Add(origin.SelectorRepresentationSha);
                if (!manifests.TryGetValue(origin.ManifestRepresentationSha, out var retainedManifest))
                {
                    byte[] content = added.TryGetValue(origin.ManifestRepresentationSha, out var pending) ? pending : await Stored(target, origin.ManifestRepresentationSha, ConfigurationLimits.Bytes, token).ConfigureAwait(true);
                    retainedManifest = ProtocolReader.Manifest(content, scope);
                    manifests.Add(origin.ManifestRepresentationSha, retainedManifest);
                }
                foreach (var reference in Wire.References(activation["closure"]))
                {
                    var asset = retainedManifest.Assets.FirstOrDefault(a => Wire.Same(a.Reference, reference));
                    if (asset == null)
                        throw new InvalidConfigurationException("graph_missing");
                    result.Add(asset.File.RepresentationSha);
                }
            }
            return result;
        }
        private async Task<CacheState> Commit(CacheNamespace target, Action<CacheState> mutate, IReadOnlyDictionary<string, byte[]> added, CancellationToken token, Action? boundary = null)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                CacheState current = await Load(target, token).ConfigureAwait(true), candidate = current.Copy();
                mutate(candidate);
                foreach (var item in added)
                    candidate.Objects[item.Key] = new CachedObject(item.Value.Length, clock.UtcSeconds);
                var protectedObjects = await Protected(target, candidate, added, token).ConfigureAwait(true);
                long protectedBytes = 0, total = 0;
                foreach (string sha in added.Keys)
                    protectedObjects.Add(sha);
                foreach (var pair in candidate.Objects)
                {
                    total = checked(total + pair.Value.Bytes);
                    if (protectedObjects.Contains(pair.Key))
                        protectedBytes = checked(protectedBytes + pair.Value.Bytes);
                }
                if (protectedBytes > ConfigurationClientPolicy.CacheBudgetBytes && added.Count != 0)
                    throw new InvalidConfigurationException("protected_capacity");
                foreach (var pair in candidate.Objects.OrderBy(p => p.Value.LastUsed).ThenBy(p => p.Key, StringComparer.Ordinal).ToArray())
                {
                    if (total <= ConfigurationClientPolicy.CacheBudgetBytes)
                        break;
                    if (!protectedObjects.Contains(pair.Key))
                    {
                        total -= pair.Value.Bytes;
                        candidate.Objects.Remove(pair.Key);
                    }
                }
                candidate.Generation = checked(current.Generation + 1);
                byte[] metadata = ProtocolReader.CacheBytes(candidate, target);
                token.ThrowIfCancellationRequested();
                boundary?.Invoke();
                StoreCommitOutcome outcome = await store.CommitAsync(target, current.Generation, metadata, added, token).ConfigureAwait(true);
                boundary?.Invoke();
                token.ThrowIfCancellationRequested();
                if (outcome == StoreCommitOutcome.Committed)
                    return candidate;
                if (outcome == StoreCommitOutcome.Unknown)
                {
                    var observed = await store.LoadAsync(target, CancellationToken.None).ConfigureAwait(true);
                    boundary?.Invoke();
                    token.ThrowIfCancellationRequested();
                    if (observed != null && observed.Generation == candidate.Generation && CanonicalJson.Sha256(observed.Metadata) == CanonicalJson.Sha256(metadata))
                        return candidate;
                }
                if (outcome == StoreCommitOutcome.Quota)
                    throw new InvalidConfigurationException("quota");
                if (outcome != StoreCommitOutcome.Conflict)
                    throw new InvalidConfigurationException("storage_failure");
            }
            throw new InvalidConfigurationException("storage_failure");
        }
        public Task<ConfigurationResult> RefreshAsync(CancellationToken cancellationToken = default)
            => RefreshInWave(null, cancellationToken);
        private Task<ConfigurationResult> RefreshInWave(NormalWave? requested, CancellationToken cancellationToken)
        {
            Alive(true);
            Task<ConfigurationResult> task;
            lock (sync)
            {
                if (normalFlight == null || normalFlight.IsCompleted)
                {
                    normalWave = requested ?? new NormalWave(checked(clock.MonotonicSeconds + 20));
                    normalFlight = RefreshNormal(normalWave);
                }
                if (requested != null && normalWave != null)
                    requested.Retries = normalWave.Retries;
                task = normalFlight;
            }
            return Wait(task, cancellationToken);
        }
        private async Task<ConfigurationResult> RefreshNormal(NormalWave wave)
        {
            using var deadline = new HostDeadline(clock, normalLifetime.Token, wave.Deadline);
            var token = deadline.Token;
            lastNormalAttempt = clock.MonotonicSeconds;
            try
            {
                var loaded = owner.Persistent ? await Load(space, token).ConfigureAwait(true) : state;
                JsonValue? previous = loaded.Normal;
                string? etag = previous == null ? null : Wire.OptionalString(previous["etag"]);
                bool rawAvailable = false;
                if (previous != null)
                {
                    try
                    {
                        await ValidateObservation(space, previous, false, token).ConfigureAwait(true);
                        rawAvailable = true;
                    }
                    catch (InvalidConfigurationException) { etag = null; }
                }
                var response = await Fetch("current.json", false, etag, false, ConfigurationLimits.Bytes, token, true, wave).ConfigureAwait(true);
                byte[] pointerBytes;
                if (response.Status == 304)
                {
                    if (!rawAvailable || previous == null)
                    {
                        Record("normal", "no_cached_validator");
                        response = await Fetch("current.json", false, null, false, ConfigurationLimits.Bytes, token, false, wave).ConfigureAwait(true);
                        if (response.Status != 200)
                            throw new InvalidConfigurationException("no_cached_validator");
                        pointerBytes = response.Bytes;
                    }
                    else
                        pointerBytes = await Stored(space, previous["pointerRepresentationSha"].AsString(), ConfigurationLimits.Bytes, token).ConfigureAwait(true);
                }
                else
                {
                    if (response.Status != 200)
                        throw new System.IO.IOException("normal_unavailable");
                    pointerBytes = response.Bytes;
                }
                var nextPointer = ProtocolReader.Pointer(pointerBytes, scope);
                string pointerSha = CanonicalJson.Sha256(pointerBytes);
                if (previous != null)
                    Accept(ProtocolReader.Sequence(previous["pointer"]["sequence"], true), previous["pointerRepresentationSha"].AsString(), nextPointer.Sequence, pointerSha);
                byte[] manifestBytes = await Immutable(nextPointer.Manifest, token, wave).ConfigureAwait(true);
                FeatureGraph.VerifyFile(manifestBytes, nextPointer.Manifest, true);
                var nextManifest = ProtocolReader.Manifest(manifestBytes, scope);
                byte[] selectorBytes = await Immutable(nextManifest.Selector, token, wave).ConfigureAwait(true);
                FeatureGraph.VerifyFile(selectorBytes, nextManifest.Selector, true);
                var nextSelector = ConfigurationReader.Read(selectorBytes, scope.App, scope.Environment);
                FeatureGraph.ValidateMetadata(nextPointer, nextManifest, nextSelector);
                var observation = Wire.Object(("pointer", nextPointer.Document), ("pointerRepresentationSha", JsonValue.String(pointerSha)), ("manifestRepresentationSha", JsonValue.String(nextPointer.Manifest.RepresentationSha)), ("selectorRepresentationSha", JsonValue.String(nextManifest.Selector.RepresentationSha)), ("etag", Wire.Nullable(response.ETag ?? etag)), ("time", Time()));
                var added = new Dictionary<string, byte[]>(StringComparer.Ordinal) { [pointerSha] = pointerBytes, [nextPointer.Manifest.RepresentationSha] = manifestBytes, [nextManifest.Selector.RepresentationSha] = selectorBytes };
                Alive(true);
                if (owner.Persistent)
                    state = await Commit(space, candidate => { if (candidate.Normal != null) Accept(ProtocolReader.Sequence(candidate.Normal["pointer"]["sequence"], true), candidate.Normal["pointerRepresentationSha"].AsString(), nextPointer.Sequence, pointerSha); candidate.Normal = observation; }, added, token).ConfigureAwait(true);
                else
                {
                    state.Normal = observation;
                    foreach (var item in added)
                        state.Objects[item.Key] = new CachedObject(item.Value.Length, clock.UtcSeconds);
                }
                Alive(true);
                normalObservedMono = clock.MonotonicSeconds;
                lock (sync)
                    releaseView = new ReleaseView(nextPointer, nextManifest, nextSelector, observation, normalObservedMono, added);
                normalFailures = 0;
                nextNormal = clock.MonotonicSeconds + Jitter(900, .2);
                Record("normal", response.Status == 304 ? "not_modified" : "fresh");
                return new ConfigurationResult("metadata_only", response.Status == 304 ? "not_modified" : "fresh", false);
            }
            catch (Exception error) when (error is InvalidConfigurationException || error is OperationCanceledException || error is System.IO.IOException || error is UnauthorizedAccessException || error is OverflowException)
            {
                normalFailures = Math.Min(6, normalFailures + 1);
                nextNormal = Math.Max(retryNormalAt, clock.MonotonicSeconds + Jitter(Math.Min(900, 30 * (1 << (normalFailures - 1))), .2));
                string reason = retired ? "owner_changed" : Reason(error);
                Record("normal", reason);
                return new ConfigurationResult(features.Count == 0 ? "bundled" : "preserved", reason, features.Count != 0);
            }
        }
        private long Jitter(int seconds, double fraction) => Math.Max(1, (long)(seconds * (1 + ((Random() * 2) - 1) * fraction)));
        private bool AdmissionDenied(FeatureSelection selection) => Restrictions.Denies("configuration.newInstance") || (selection.Experiment != null && Restrictions.Denies("configuration.newEnrollment"));
        private void AdmissionBoundary(ReleaseView view, FeatureSelection selection)
        {
            Alive(true);
            if (AdmissionDenied(selection))
                throw new InvalidConfigurationException("admission_paused");
            if (selection.Experiment != null && !Fresh(view.Observation, view.ObservedMono))
                throw new InvalidConfigurationException(FreshnessReason(view.Observation, view.ObservedMono));
        }
        public async Task<ConfigurationResult> GetFeatureAsync(string featureKey, IReadOnlyDictionary<string, ContextAttribute>? attributes = null, CancellationToken cancellationToken = default)
        {
            Alive(true);
            Wire.Text(featureKey);
            var wave = new NormalWave(checked(clock.MonotonicSeconds + 20));
            using var caller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, normalLifetime.Token);
            using var linked = new HostDeadline(clock, caller.Token, wave.Deadline);
            var token = linked.Token;
            await featureGate.WaitAsync(token).ConfigureAwait(true);
            try
            {
                Alive(true);
                token.ThrowIfCancellationRequested();
                ReleaseView? view;
                lock (sync)
                    view = releaseView;
                if (view == null)
                {
                    var refresh = await RefreshInWave(wave, token).ConfigureAwait(true);
                    lock (sync)
                        view = releaseView;
                    if (view == null)
                        return Fallback(featureKey, refresh.Reason);
                }
                var selected = Evaluator.Evaluate(view.Selector, featureKey, new EvaluationContext(owner.SubjectId, clock.UtcSeconds, attributes ?? new Dictionary<string, ContextAttribute>()));
                if (selected.Choice == null)
                    return new ConfigurationResult("rejected", "missing_feature", false);
                var frozenSelection = new FeatureSelection(selected);
                if (AdmissionDenied(frozenSelection))
                    return Default(featureKey, "unavailable");
                if (selected.Experiment != null && (!owner.Persistent || !Fresh(view.Observation, view.ObservedMono)))
                    return Default(featureKey, !owner.Persistent ? "ephemeral_identity" : FreshnessReason(view.Observation, view.ObservedMono));
                var graph = FeatureGraph.Chosen(view.Manifest, featureKey, selected.Choice);
                var added = view.Metadata.ToDictionary(p => p.Key, p => (byte[])p.Value.Clone(), StringComparer.Ordinal);
                JsonValue? payload = null;
                // At most two ordinary immutable requests are in flight; validate before commit.
                var content = await Task.WhenAll(graph.Select(a => Immutable(a.File, token, wave))).ConfigureAwait(true);
                for (int i = 0; i < graph.Count; i++)
                {
                    var item = FeatureGraph.Asset(content[i], scope, graph[i]);
                    added[graph[i].File.RepresentationSha] = content[i];
                    if (graph[i].Reference.Key == featureKey && graph[i].Reference.Revision == selected.Choice.Revision)
                        payload = item;
                }
                var context = new FeatureContext(space, featureKey, frozenSelection, new FeatureOrigin(view.Pointer.Sequence, view.Pointer.Manifest.RepresentationSha, view.Manifest.Selector.RepresentationSha), graph.Select(a => a.Reference), payload ?? throw new InvalidConfigurationException("graph_missing"), owner.Persistent);
                AdmissionBoundary(view, frozenSelection);
                if (owner.Persistent)
                {
                    state = await Commit(space, candidate => { AdmissionBoundary(view, frozenSelection); candidate.Activations[featureKey] = FeatureGraph.Activation(context); }, added, token, () => AdmissionBoundary(view, frozenSelection)).ConfigureAwait(true);
                }
                AdmissionBoundary(view, frozenSelection);
                token.ThrowIfCancellationRequested();
                features[featureKey] = context;
                Record("feature", "activated");
                return new ConfigurationResult(owner.Persistent ? "available" : "ephemeral", owner.Persistent ? "activated" : "ephemeral_identity", owner.Persistent, context);
            }
            catch (Exception error) when (error is InvalidConfigurationException || error is OperationCanceledException || error is System.IO.IOException || error is UnauthorizedAccessException || error is OverflowException) { if (error is InvalidConfigurationException && error.Message == "admission_paused") return Default(featureKey, "unavailable"); if (error is InvalidConfigurationException && (error.Message == "unknown_time" || error.Message == "stale")) return Default(featureKey, error.Message); string reason = retired ? "owner_changed" : Reason(error); Record("feature", reason); return Fallback(featureKey, reason); }
            finally { featureGate.Release(); }
        }
        public Task<ConfigurationResult> RefreshControlsAsync(CancellationToken cancellationToken = default)
        {
            Alive(false);
            Task<ConfigurationResult> task;
            lock (sync)
            {
                if (controlFlight == null || controlFlight.IsCompleted)
                    controlFlight = RefreshControls();
                task = controlFlight;
            }
            return Wait(task, cancellationToken);
        }
        private async Task<ConfigurationResult> RefreshControls()
        {
            var token = publicLifetime.Token;
            await controlMemory.Gate.WaitAsync(token).ConfigureAwait(true);
            try
            {
                publicState = await Load(publicSpace, token).ConfigureAwait(true);
                JsonValue? previous = publicState.Control;
                string? etag = previous == null ? null : Wire.OptionalString(previous["etag"]);
                bool rawAvailable = false;
                if (previous != null)
                {
                    try
                    {
                        await ValidateObservation(publicSpace, previous, true, token).ConfigureAwait(true);
                        rawAvailable = true;
                    }
                    catch (InvalidConfigurationException) { etag = null; }
                }
                var response = await Fetch("control.json", true, etag, false, ConfigurationClientPolicy.ControlBytes, token, false).ConfigureAwait(true);
                byte[] raw;
                if (response.Status == 304 && rawAvailable && previous != null)
                    raw = await Stored(publicSpace, previous["representationSha"].AsString(), ConfigurationClientPolicy.ControlBytes, token).ConfigureAwait(true);
                else
                {
                    if (response.Status == 304)
                    {
                        Record("control", "no_cached_validator");
                        response = await Fetch("control.json", true, null, false, ConfigurationClientPolicy.ControlBytes, token, false).ConfigureAwait(true);
                    }
                    if (response.Status != 200)
                        throw new System.IO.IOException("control_unavailable");
                    raw = response.Bytes;
                }
                var document = ProtocolReader.Control(raw, scope);
                string sha = CanonicalJson.Sha256(raw);
                OperationalRestrictions old = Restrictions;
                Accept(old.Sequence, old.RepresentationSha, document.Sequence, sha);
                if (previous != null)
                    Accept(ProtocolReader.Sequence(previous["document"]["sequence"], true), previous["representationSha"].AsString(), document.Sequence, sha);
                var observation = Wire.Object(("document", document.Document), ("representationSha", JsonValue.String(sha)), ("etag", Wire.Nullable(response.ETag ?? etag)), ("time", Time()));
                var added = new Dictionary<string, byte[]>(StringComparer.Ordinal) { [sha] = raw };
                if (document.Sequence != old.Sequence || !old.Durable)
                    controlMemory.Observe(new OperationalRestrictions(document.Sequence, sha, false, new Dictionary<string, int>(document.Restrictions)));
                try
                {
                    publicState = await Commit(publicSpace, candidate => { if (candidate.Control != null) Accept(ProtocolReader.Sequence(candidate.Control["document"]["sequence"], true), candidate.Control["representationSha"].AsString(), document.Sequence, sha); candidate.Control = observation; }, added, token).ConfigureAwait(true);
                    controlMemory.Observe(new OperationalRestrictions(document.Sequence, sha, true, new Dictionary<string, int>(document.Restrictions)));
                }
                catch (InvalidConfigurationException error) when (error.Message == "storage_failure" || error.Message == "protected_capacity" || error.Message == "quota")
                {
                    controlMemory.Observe(new OperationalRestrictions(document.Sequence, sha, false, new Dictionary<string, int>(document.Restrictions)));
                    throw;
                }
                controlObservedMono = clock.MonotonicSeconds;
                controlFailures = 0;
                nextControl = clock.MonotonicSeconds + Jitter(60, .1);
                Record("control", "fresh");
                return new ConfigurationResult("available", response.Status == 304 ? "not_modified" : "fresh", true);
            }
            catch (Exception error) when (error is InvalidConfigurationException || error is OperationCanceledException || error is System.IO.IOException || error is UnauthorizedAccessException || error is OverflowException) { controlFailures = Math.Min(3, controlFailures + 1); nextControl = Math.Max(retryControlAt, clock.MonotonicSeconds + Jitter(controlFailures == 1 ? 15 : controlFailures == 2 ? 30 : 60, .1)); string reason = Reason(error); Record("control", reason); return new ConfigurationResult("preserved", reason, Restrictions.Durable); }
            finally { controlMemory.Gate.Release(); }
        }
        public async Task<ConfigurationResult> GetPinnedAsync(string pinId, CancellationToken cancellationToken = default)
        {
            Alive(true);
            Wire.Uuid(pinId);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, normalLifetime.Token);
            await featureGate.WaitAsync(linked.Token).ConfigureAwait(true);
            try
            {
                Alive(true);
                linked.Token.ThrowIfCancellationRequested();
                if (owner.Persistent)
                    await Load(space, linked.Token).ConfigureAwait(true);
                Alive(true);
                linked.Token.ThrowIfCancellationRequested();
                if (pins.TryGetValue(pinId, out var context))
                    return new ConfigurationResult("available", Fresh(state.Normal, normalObservedMono) ? "fresh" : FreshnessReason(state.Normal, normalObservedMono), true, context);
                return new ConfigurationResult("rejected", "missing_feature", false);
            }
            finally { featureGate.Release(); }
        }
        public async Task<ConfigurationResult> PinAsync(FeatureContext context, string pinId, CancellationToken cancellationToken = default)
        {
            Alive(true);
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            Wire.Uuid(pinId);
            if (!owner.Persistent || !context.Durable || context.NamespaceKey != space.Key || context.OwnerGeneration != owner.Generation)
                return new ConfigurationResult("rejected", "owner_changed", false);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, normalLifetime.Token);
            await featureGate.WaitAsync(linked.Token).ConfigureAwait(true);
            try
            {
                Alive(true);
                linked.Token.ThrowIfCancellationRequested();
                var pin = Wire.Object(("pinId", JsonValue.String(pinId)), ("activation", FeatureGraph.Activation(context)), ("createdAtSeconds", JsonValue.Integer(clock.UtcSeconds)));
                if (state.Pins.TryGetValue(pinId, out var stored) && CanonicalJson.SemanticSha256(stored["activation"]) == CanonicalJson.SemanticSha256(pin["activation"]))
                {
                    pins[pinId] = context;
                    return new ConfigurationResult("available", "activated", true, context);
                }
                state = await Commit(space, candidate => { if (candidate.Pins.TryGetValue(pinId, out var prior)) { if (CanonicalJson.SemanticSha256(prior["activation"]) != CanonicalJson.SemanticSha256(pin["activation"])) throw new InvalidConfigurationException("sequence_conflict"); } else candidate.Pins.Add(pinId, pin); }, new Dictionary<string, byte[]>(), linked.Token).ConfigureAwait(true);
                Alive(true);
                pins[pinId] = context;
                return new ConfigurationResult("available", "activated", true, context);
            }
            catch (Exception error) when (error is InvalidConfigurationException || error is System.IO.IOException || error is OperationCanceledException || error is OverflowException)
            {
                string reason = Reason(error);
                if (retired || disposed || reason == "owner_changed")
                    return new ConfigurationResult("rejected", "owner_changed", false);
                return new ConfigurationResult("preserved", reason, context.Durable, context);
            }
            finally { featureGate.Release(); }
        }
        public async Task<bool> ReleasePinAsync(string pinId, CancellationToken cancellationToken = default)
        {
            Alive(true);
            Wire.Uuid(pinId);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, normalLifetime.Token);
            await featureGate.WaitAsync(linked.Token).ConfigureAwait(true);
            try
            {
                state = await Commit(space, candidate => candidate.Pins.Remove(pinId), new Dictionary<string, byte[]>(), linked.Token).ConfigureAwait(true);
                Alive(true);
                pins.Remove(pinId);
                return true;
            }
            catch (InvalidConfigurationException) { return false; }
            finally { featureGate.Release(); }
        }
        public async Task TickAsync(bool foreground, CancellationToken cancellationToken = default)
        {
            Alive(false);
            if (!foreground)
                return;
            long now = clock.MonotonicSeconds;
            Task<ConfigurationResult>? control = now >= nextControl ? RefreshControlsAsync(cancellationToken) : null;
            if (!retired && now >= nextNormal && (lastNormalAttempt == long.MinValue || now - lastNormalAttempt >= 60))
                await RefreshAsync(cancellationToken).ConfigureAwait(true);
            if (control != null)
                await control.ConfigureAwait(true);
        }
    }
}
