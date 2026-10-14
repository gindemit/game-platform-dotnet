using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Configuration.Client
{
    public sealed class ConfigurationScope
    {
        public string App { get; }
        public string Environment { get; }
        public ConfigurationScope(string app, string environment)
        {
            Wire.Text(app);
            Wire.Text(environment);
            App = app;
            Environment = environment;
        }
    }
    public sealed class ConfigurationOwner
    {
        public string OwnerId { get; }
        public string SubjectId { get; }
        public long Generation { get; }
        public bool Persistent { get; }
        public ConfigurationOwner(string ownerId, string subjectId, long generation, bool persistent = true)
        {
            Wire.Text(ownerId);
            Wire.Text(subjectId, false);
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            OwnerId = ownerId;
            SubjectId = subjectId;
            Generation = generation;
            Persistent = persistent;
        }
    }
    public sealed class CacheNamespace
    {
        public ConfigurationScope Scope { get; }
        public ConfigurationOwner? Owner { get; }
        public string Key { get; }
        public CacheNamespace(ConfigurationScope scope, ConfigurationOwner? owner)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            Owner = owner;
            Key = CanonicalJson.SemanticSha256(JsonValue.Array(new[] { JsonValue.String(scope.App), JsonValue.String(scope.Environment), owner == null ? JsonValue.Null : JsonValue.String(owner.OwnerId), owner == null ? JsonValue.Null : JsonValue.String(owner.SubjectId) }));
        }
    }
    public sealed class ConfigurationHttpRequest
    {
        public Uri Address { get; }
        public int MaximumDecodedBytes { get; }
        public string? ETag { get; }
        public bool Immutable { get; }
        public ConfigurationHttpRequest(Uri address, int maximumDecodedBytes, string? etag, bool immutable)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            if (maximumDecodedBytes < 1 || maximumDecodedBytes > ConfigurationLimits.Bytes)
                throw new ArgumentOutOfRangeException(nameof(maximumDecodedBytes));
            MaximumDecodedBytes = maximumDecodedBytes;
            ETag = etag;
            Immutable = immutable;
        }
    }
    public sealed class ConfigurationHttpResponse
    {
        private readonly byte[] bytes;
        public int Status { get; }
        public byte[] Bytes => (byte[])bytes.Clone();
        public string? ETag { get; }
        public int? RetryAfterSeconds { get; }
        public Uri Address { get; }
        public string ContentType { get; }
        public ConfigurationHttpResponse(int status, byte[] bytes, Uri address, string contentType = "application/json", string? etag = null, int? retryAfterSeconds = null)
        {
            Status = status;
            this.bytes = (byte[])(bytes ?? throw new ArgumentNullException(nameof(bytes))).Clone();
            Address = address ?? throw new ArgumentNullException(nameof(address));
            ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
            ETag = etag;
            if (retryAfterSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(retryAfterSeconds));
            RetryAfterSeconds = retryAfterSeconds;
        }
    }
    public interface IConfigurationTransport
    {
        Task<ConfigurationHttpResponse> FetchAsync(ConfigurationHttpRequest request, CancellationToken cancellationToken);
    }
    public interface IConfigurationClock
    {
        long UtcSeconds { get; }
        long MonotonicSeconds { get; }
        bool IsTrusted { get; }
        Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }
    public interface IConfigurationRandom
    {
        double NextDouble();
    }
    public sealed class StoreSnapshot
    {
        private readonly byte[] metadata;
        public long Generation { get; }
        public byte[] Metadata => (byte[])metadata.Clone();
        public StoreSnapshot(long generation, byte[] metadata)
        {
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            Generation = generation;
            this.metadata = (byte[])(metadata ?? throw new ArgumentNullException(nameof(metadata))).Clone();
        }
    }
    public enum StoreCommitOutcome
    {
        Committed, Conflict, Unavailable, Unknown, Quota
    }
    public interface IConfigurationStore
    {
        Task<StoreSnapshot?> LoadAsync(CacheNamespace space, CancellationToken cancellationToken);
        Task<byte[]?> ReadObjectAsync(CacheNamespace space, string representationSha, int maximumDecodedBytes, CancellationToken cancellationToken);
        Task<StoreCommitOutcome> CommitAsync(CacheNamespace space, long expectedGeneration, byte[] metadata, IReadOnlyDictionary<string, byte[]> objects, CancellationToken cancellationToken);
    }
    public sealed class FeatureOrigin
    {
        public long ReleaseSequence { get; }
        public string ManifestRepresentationSha { get; }
        public string SelectorRepresentationSha { get; }
        public FeatureOrigin(long releaseSequence, string manifestRepresentationSha, string selectorRepresentationSha)
        {
            if (releaseSequence < 1)
                throw new ArgumentOutOfRangeException(nameof(releaseSequence));
            Wire.Digest(manifestRepresentationSha);
            Wire.Digest(selectorRepresentationSha);
            ReleaseSequence = releaseSequence;
            ManifestRepresentationSha = manifestRepresentationSha;
            SelectorRepresentationSha = selectorRepresentationSha;
        }
        public JsonValue ToJson() => Wire.Object(("releaseSequence", Wire.Decimal(ReleaseSequence)), ("manifestRepresentationSha", JsonValue.String(ManifestRepresentationSha)), ("selectorRepresentationSha", JsonValue.String(SelectorRepresentationSha)));
    }
    public sealed class FeatureSelection
    {
        public Choice Choice { get; }
        public string Reason { get; }
        public string? RuleId { get; }
        public int? Bucket { get; }
        public ExperimentReference? Experiment { get; }
        internal FeatureSelection(Choice choice, string reason, string? ruleId, int? bucket, ExperimentReference? experiment)
        {
            Choice = choice;
            Reason = reason;
            RuleId = ruleId;
            Bucket = bucket;
            Experiment = experiment;
        }
        internal FeatureSelection(Selection selection) : this(selection.Choice ?? throw new InvalidConfigurationException("missing_feature"), selection.Reason, selection.RuleId, selection.Bucket, selection.Experiment) { }
    }
    public sealed class FeatureContext
    {
        public string FeatureKey { get; }
        public FeatureSelection Selection { get; }
        public FeatureOrigin Origin { get; }
        public IReadOnlyList<FeatureRevisionReference> Closure { get; }
        public JsonValue Payload { get; }
        public bool Durable { get; }
        internal string NamespaceKey { get; }
        internal long OwnerGeneration { get; }
        internal FeatureContext(CacheNamespace binding, string key, FeatureSelection selection, FeatureOrigin origin, IEnumerable<FeatureRevisionReference> closure, JsonValue payload, bool durable)
        {
            if (binding.Owner == null)
                throw new InvalidConfigurationException("owner_changed");
            NamespaceKey = binding.Key;
            OwnerGeneration = binding.Owner.Generation;
            FeatureKey = key;
            Selection = selection;
            Origin = origin;
            Closure = new List<FeatureRevisionReference>(closure).AsReadOnly();
            Payload = payload;
            Durable = durable;
        }
        public JsonValue FreezeEventContext() => Wire.Object(("selection", ProtocolReader.SelectionJson(FeatureKey, Selection)), ("origin", Origin.ToJson()), ("closure", Wire.References(Closure)));
    }
    public sealed class ConfigurationResult
    {
        public string Status { get; }
        public string Reason { get; }
        public bool Durable { get; }
        public FeatureContext? Feature { get; }
        public Choice? BundledChoice { get; }
        public ConfigurationResult(string status, string reason, bool durable, FeatureContext? feature = null, Choice? bundledChoice = null)
        {
            Status = status;
            Reason = reason;
            Durable = durable;
            Feature = feature;
            BundledChoice = bundledChoice;
        }
    }
    public sealed class ConfigurationDiagnostic
    {
        public string Channel { get; }
        public string Reason { get; }
        public long AtSeconds { get; }
        public ConfigurationDiagnostic(string channel, string reason, long atSeconds)
        {
            Channel = channel;
            Reason = reason;
            AtSeconds = atSeconds;
        }
    }
    public sealed class OperationalRestrictions
    {
        private readonly IReadOnlyDictionary<string, int> values;
        public long Sequence { get; }
        public string RepresentationSha { get; }
        public bool Durable { get; }
        internal OperationalRestrictions(long sequence, string digest, bool durable, IDictionary<string, int> restrictions)
        {
            Sequence = sequence;
            RepresentationSha = digest;
            Durable = durable;
            values = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(restrictions, StringComparer.Ordinal));
        }
        public bool Denies(string key) => values.TryGetValue(key, out var rate) && rate == 0;
        public int CapPermille(string key, int ordinaryRate)
        {
            if (key != "analytics.sample")
                throw new ArgumentException("sampling_key_invalid", nameof(key));
            if (ordinaryRate < 0 || ordinaryRate > 1000)
                throw new ArgumentOutOfRangeException(nameof(ordinaryRate));
            return values.TryGetValue(key, out var rate) ? Math.Min(ordinaryRate, rate) : ordinaryRate;
        }
        internal IReadOnlyDictionary<string, int> Values => values;
    }
    /// <summary>Explicit app/environment lifetime; retain across every owner/client replacement.</summary>
    public sealed class ConfigurationControlMemory
    {
        private readonly object sync = new object();
        private OperationalRestrictions current = new OperationalRestrictions(0, string.Empty, false, new Dictionary<string, int>());
        public ConfigurationScope Scope { get; }
        public OperationalRestrictions Current
        {
            get
            {
                lock (sync)
                    return current;
            }
        }
        internal SemaphoreSlim Gate { get; } = new SemaphoreSlim(1, 1);
        public ConfigurationControlMemory(ConfigurationScope scope)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }
        internal void Observe(OperationalRestrictions candidate, bool loadedFromStore = false)
        {
            lock (sync)
            {
                if (candidate.Sequence < current.Sequence)
                {
                    if (loadedFromStore)
                        return;
                    throw new InvalidConfigurationException("lower_sequence");
                }
                if (candidate.Sequence == current.Sequence && candidate.RepresentationSha != current.RepresentationSha)
                    throw new InvalidConfigurationException("sequence_conflict");
                if (candidate.Durable)
                {
                    current = candidate;
                    return;
                }
                var merged = new Dictionary<string, int>(current.Values, StringComparer.Ordinal);
                foreach (var pair in candidate.Values)
                    if (!merged.TryGetValue(pair.Key, out int rate) || pair.Value < rate)
                        merged[pair.Key] = pair.Value;
                current = new OperationalRestrictions(candidate.Sequence, candidate.RepresentationSha, false, merged);
            }
        }
    }
}
