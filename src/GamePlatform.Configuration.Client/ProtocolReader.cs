using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GamePlatform.Configuration.Client
{
    internal static class Wire
    {
        internal static JsonValue Object(params (string Key, JsonValue Value)[] fields)
        {
            var values = new List<KeyValuePair<string, JsonValue>>();
            foreach (var field in fields)
                values.Add(new KeyValuePair<string, JsonValue>(field.Key, field.Value));
            return JsonValue.Object(values);
        }
        internal static JsonValue Decimal(long value) => JsonValue.String(value.ToString(CultureInfo.InvariantCulture));
        internal static JsonValue Nullable(string? value) => value == null ? JsonValue.Null : JsonValue.String(value);
        internal static string Text(string value, bool nonempty = true)
        {
            if (value == null || (nonempty && value.Length == 0) || new UTF8Encoding(false, true).GetByteCount(value) > ConfigurationLimits.StringBytes)
                throw new InvalidConfigurationException("string_invalid");
            return value;
        }
        internal static string String(JsonValue value, bool nonempty = true) => Text(value.AsString(), nonempty);
        internal static string? OptionalString(JsonValue value) => value.Kind == JsonKind.Null ? null : String(value);
        internal static void Shape(JsonValue value, params string[] fields)
        {
            var map = value.AsObject();
            if (map.Count != fields.Length)
                throw new InvalidConfigurationException("shape_invalid");
            foreach (var key in fields)
                if (!map.ContainsKey(key))
                    throw new InvalidConfigurationException("shape_invalid");
        }
        internal static void Scope(JsonValue value, ConfigurationScope scope, string schema)
        {
            if (String(value["schema"]) != schema)
                throw new InvalidConfigurationException("schema_invalid");
            if (String(value["app"]) != scope.App || String(value["environment"]) != scope.Environment)
                throw new InvalidConfigurationException("scope_mismatch");
        }
        internal static long Time(JsonValue value)
        {
            long number = value.AsInteger();
            if (number < 0)
                throw new InvalidConfigurationException("time_invalid");
            return number;
        }
        internal static string Digest(string value)
        {
            if (value.Length != 64)
                throw new InvalidConfigurationException("digest_invalid");
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'f') && !(c >= '0' && c <= '9'))
                    throw new InvalidConfigurationException("digest_invalid");
            return value;
        }
        internal static string Digest(JsonValue value) => Digest(String(value));
        internal static string Identity(FeatureRevisionReference value) => CanonicalJson.SemanticSha256(JsonValue.Array(new[] { JsonValue.String(value.Key), JsonValue.String(value.Revision) }));
        internal static bool Same(FeatureRevisionReference a, FeatureRevisionReference b) => a.Key == b.Key && a.Revision == b.Revision && a.SemanticSha == b.SemanticSha;
        internal static FeatureRevisionReference Reference(JsonValue value)
        {
            Shape(value, "key", "revision", "semanticSha");
            return new FeatureRevisionReference(String(value["key"]), String(value["revision"]), Digest(value["semanticSha"]));
        }
        internal static JsonValue Reference(FeatureRevisionReference value) => Object(("key", JsonValue.String(value.Key)), ("revision", JsonValue.String(value.Revision)), ("semanticSha", JsonValue.String(value.SemanticSha)));
        internal static IReadOnlyList<FeatureRevisionReference> References(JsonValue value, bool uniqueKeys = false)
        {
            var result = new List<FeatureRevisionReference>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in value.AsArray())
            {
                var reference = Reference(item);
                if (!seen.Add(uniqueKeys ? reference.Key : Identity(reference)))
                    throw new InvalidConfigurationException("reference_duplicate");
                result.Add(reference);
            }
            if (result.Count > 64)
                throw new InvalidConfigurationException("graph_bounds");
            return result.AsReadOnly();
        }
        internal static JsonValue References(IEnumerable<FeatureRevisionReference> values)
        {
            var result = new List<JsonValue>();
            foreach (var value in values)
                result.Add(Reference(value));
            return JsonValue.Array(result);
        }
        internal static void Path(string path)
        {
            string[] pieces = path.Split('/');
            if (pieces.Length != 2 || (pieces[0] != "assets" && pieces[0] != "selectors" && pieces[0] != "manifests") || pieces[1].Length != 69 || !pieces[1].EndsWith(".json", StringComparison.Ordinal))
                throw new InvalidConfigurationException("path_invalid");
            Digest(pieces[1].Substring(0, 64));
        }
        internal static void Uuid(string value)
        {
            if (value.Length != 36 || value[14] != '7' || "89ab".IndexOf(value[19]) < 0)
                throw new InvalidConfigurationException("uuid_invalid");
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i == 8 || i == 13 || i == 18 || i == 23)
                {
                    if (c != '-')
                        throw new InvalidConfigurationException("uuid_invalid");
                }
                else if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f'))
                    throw new InvalidConfigurationException("uuid_invalid");
            }
        }
    }
    public sealed class ImmutableFileReference
    {
        public string Path { get; }
        public string RepresentationSha { get; }
        public string SemanticSha { get; }
        public int DecodedBytes { get; }
        internal ImmutableFileReference(string path, string representationSha, string semanticSha, int decodedBytes)
        {
            Path = path;
            RepresentationSha = representationSha;
            SemanticSha = semanticSha;
            DecodedBytes = decodedBytes;
        }
    }
    public sealed class ReleasePointer
    {
        public long Sequence { get; }
        public long IssuedAtSeconds { get; }
        public string Release { get; }
        public ImmutableFileReference Manifest { get; }
        public JsonValue Document { get; }
        internal ReleasePointer(long sequence, long issued, string release, ImmutableFileReference manifest, JsonValue document)
        {
            Sequence = sequence;
            IssuedAtSeconds = issued;
            Release = release;
            Manifest = manifest;
            Document = document;
        }
    }
    public sealed class AssetDescriptor
    {
        public FeatureRevisionReference Reference { get; }
        public ImmutableFileReference File { get; }
        public IReadOnlyList<FeatureRevisionReference> Dependencies { get; }
        internal AssetDescriptor(FeatureRevisionReference reference, ImmutableFileReference file, IReadOnlyList<FeatureRevisionReference> dependencies)
        {
            Reference = reference;
            File = file;
            Dependencies = dependencies;
        }
    }
    public sealed class ReleaseManifest
    {
        public string Release { get; }
        public ImmutableFileReference Selector { get; }
        public IReadOnlyList<AssetDescriptor> Assets { get; }
        public JsonValue Document { get; }
        internal ReleaseManifest(string release, ImmutableFileReference selector, IReadOnlyList<AssetDescriptor> assets, JsonValue document)
        {
            Release = release;
            Selector = selector;
            Assets = assets;
            Document = document;
        }
    }
    public sealed class ControlSnapshot
    {
        public long Sequence { get; }
        public JsonValue Document { get; }
        public IReadOnlyDictionary<string, int> Restrictions { get; }
        internal ControlSnapshot(long sequence, JsonValue document, Dictionary<string, int> restrictions)
        {
            Sequence = sequence;
            Document = document;
            Restrictions = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(restrictions);
        }
    }
    internal sealed class CachedObject
    {
        internal int Bytes; internal long LastUsed;
        internal CachedObject(int bytes, long lastUsed)
        {
            Bytes = bytes;
            LastUsed = lastUsed;
        }
    }
    internal sealed class CacheState
    {
        internal long Generation;
        internal long OwnerGeneration;
        internal JsonValue? Normal;
        internal JsonValue? Control;
        internal readonly Dictionary<string, CachedObject> Objects = new Dictionary<string, CachedObject>(StringComparer.Ordinal);
        internal readonly Dictionary<string, JsonValue> Activations = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
        internal readonly Dictionary<string, JsonValue> Pins = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
        internal CacheState Copy()
        {
            var copy = new CacheState { Generation = Generation, OwnerGeneration = OwnerGeneration, Normal = Normal, Control = Control };
            foreach (var pair in Objects)
                copy.Objects.Add(pair.Key, new CachedObject(pair.Value.Bytes, pair.Value.LastUsed));
            foreach (var pair in Activations)
                copy.Activations.Add(pair.Key, pair.Value);
            foreach (var pair in Pins)
                copy.Pins.Add(pair.Key, pair.Value);
            return copy;
        }
    }
    public static class ProtocolReader
    {
        public static long Sequence(JsonValue value, bool positive = false)
        {
            string text = value.AsString();
            if (text.Length == 0 || text.Length > 19 || (text.Length > 1 && text[0] == '0'))
                throw new InvalidConfigurationException("sequence_invalid");
            foreach (char c in text)
                if (c < '0' || c > '9')
                    throw new InvalidConfigurationException("sequence_invalid");
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || (positive && result == 0))
                throw new InvalidConfigurationException("sequence_invalid");
            return result;
        }
        private static ImmutableFileReference File(JsonValue value, string? category = null)
        {
            Wire.Shape(value, "path", "representationSha", "semanticSha", "decodedBytes");
            return FileFields(value, category);
        }
        private static ImmutableFileReference FileFields(JsonValue value, string? category)
        {
            string path = Wire.String(value["path"]), representation = Wire.Digest(value["representationSha"]), semantic = Wire.Digest(value["semanticSha"]);
            long size = value["decodedBytes"].AsInteger();
            Wire.Path(path);
            if (size < 1 || size > ConfigurationLimits.Bytes || (category != null && path != category + "/" + representation + ".json"))
                throw new InvalidConfigurationException("file_invalid");
            return new ImmutableFileReference(path, representation, semantic, checked((int)size));
        }
        public static ReleasePointer Pointer(byte[] bytes, ConfigurationScope scope)
        {
            var value = CanonicalJson.Parse(bytes);
            Wire.Shape(value, "schema", "app", "environment", "sequence", "issuedAtSeconds", "release", "manifest");
            Wire.Scope(value, scope, "platform.remote-config.current.v1");
            return new ReleasePointer(Sequence(value["sequence"], true), Wire.Time(value["issuedAtSeconds"]), Wire.String(value["release"]), File(value["manifest"], "manifests"), value);
        }
        public static ReleaseManifest Manifest(byte[] bytes, ConfigurationScope scope)
        {
            var value = CanonicalJson.Parse(bytes);
            Wire.Shape(value, "schema", "app", "environment", "release", "issuedAtSeconds", "selector", "assets", "retained");
            Wire.Scope(value, scope, "platform.remote-config.release.v1");
            Wire.Time(value["issuedAtSeconds"]);
            var assets = new List<AssetDescriptor>();
            var identities = new Dictionary<string, AssetDescriptor>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            string? previous = null;
            foreach (var item in value["assets"].AsArray())
            {
                Wire.Shape(item, "key", "revision", "semanticSha", "path", "representationSha", "decodedBytes", "dependencies");
                var reference = new FeatureRevisionReference(Wire.String(item["key"]), Wire.String(item["revision"]), Wire.Digest(item["semanticSha"]));
                var file = FileFields(item, null);
                string id = Wire.Identity(reference), order = CanonicalIdentity(reference);
                if (file.Path != "assets/" + id + ".json" || identities.ContainsKey(id) || !paths.Add(file.Path) || (previous != null && string.CompareOrdinal(previous, order) >= 0))
                    throw new InvalidConfigurationException("asset_identity");
                previous = order;
                var descriptor = new AssetDescriptor(reference, file, Wire.References(item["dependencies"], true));
                assets.Add(descriptor);
                identities.Add(id, descriptor);
            }
            if (assets.Count > 4096)
                throw new InvalidConfigurationException("manifest_bounds");
            previous = null;
            var retained = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in value["retained"].AsArray())
            {
                var reference = Wire.Reference(item);
                string id = Wire.Identity(reference), order = CanonicalIdentity(reference);
                if (!retained.Add(id) || !identities.TryGetValue(id, out var found) || !Wire.Same(reference, found.Reference) || (previous != null && string.CompareOrdinal(previous, order) >= 0))
                    throw new InvalidConfigurationException("retained_invalid");
                previous = order;
            }
            if (retained.Count > 4096)
                throw new InvalidConfigurationException("manifest_bounds");
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var done = new HashSet<string>(StringComparer.Ordinal);
            void Walk(AssetDescriptor descriptor)
            {
                string id = Wire.Identity(descriptor.Reference);
                if (visiting.Contains(id))
                    throw new InvalidConfigurationException("graph_cycle");
                if (done.Contains(id))
                    return;
                visiting.Add(id);
                foreach (var dependency in descriptor.Dependencies)
                {
                    if (!identities.TryGetValue(Wire.Identity(dependency), out var child) || !Wire.Same(child.Reference, dependency))
                        throw new InvalidConfigurationException("graph_missing");
                    Walk(child);
                }
                visiting.Remove(id);
                done.Add(id);
            }
            foreach (var asset in assets)
                Walk(asset);
            return new ReleaseManifest(Wire.String(value["release"]), File(value["selector"], "selectors"), assets.AsReadOnly(), value);
        }
        private static string CanonicalIdentity(FeatureRevisionReference reference) => Encoding.UTF8.GetString(CanonicalJson.Serialize(JsonValue.Array(new[] { JsonValue.String(reference.Key), JsonValue.String(reference.Revision) })));
        public static ControlSnapshot Control(byte[] bytes, ConfigurationScope scope)
        {
            if (bytes.Length > ConfigurationClientPolicy.ControlBytes)
                throw new InvalidConfigurationException("control_bounds");
            var value = CanonicalJson.Parse(bytes);
            Wire.Shape(value, "schema", "app", "environment", "sequence", "issuedAtSeconds", "restrictions");
            Wire.Scope(value, scope, "platform.remote-config.control.v1");
            Wire.Time(value["issuedAtSeconds"]);
            var restrictions = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in value["restrictions"].AsArray())
            {
                Wire.Shape(item, "key", "effect", "ratePermille");
                string key = Wire.String(item["key"]), effect = Wire.String(item["effect"]);
                if (key != "analytics.collect" && key != "analytics.send" && key != "analytics.sample" && key != "configuration.newEnrollment" && key != "configuration.newInstance")
                    throw new InvalidConfigurationException("restriction_unknown");
                int rate;
                if (effect == "deny" && item["ratePermille"].Kind == JsonKind.Null)
                    rate = 0;
                else if (effect == "cap" && key == "analytics.sample" && item["ratePermille"].Kind == JsonKind.Integer && item["ratePermille"].AsInteger() >= 0 && item["ratePermille"].AsInteger() <= 1000)
                    rate = (int)item["ratePermille"].AsInteger();
                else
                    throw new InvalidConfigurationException("restriction_invalid");
                if (restrictions.ContainsKey(key))
                    throw new InvalidConfigurationException("restriction_duplicate");
                restrictions.Add(key, rate);
            }
            return new ControlSnapshot(Sequence(value["sequence"], true), value, restrictions);
        }
        internal static JsonValue SelectionJson(string key, FeatureSelection selection)
        {
            var choice = selection.Choice;
            return Wire.Object(("featureKey", JsonValue.String(key)), ("reference", Wire.Reference(new FeatureRevisionReference(key, choice.Revision, choice.SemanticSha))), ("variant", JsonValue.String(choice.Variant)), ("value", choice.Value), ("reason", JsonValue.String(selection.Reason)), ("ruleId", Wire.Nullable(selection.RuleId)), ("bucket", selection.Bucket.HasValue ? JsonValue.Integer(selection.Bucket.Value) : JsonValue.Null), ("experimentKey", Wire.Nullable(selection.Experiment?.Key)), ("experimentVersion", Wire.Nullable(selection.Experiment?.Version)), ("dependencies", Wire.References(choice.Dependencies)));
        }
        internal static FeatureSelection ReadSelection(JsonValue value)
        {
            Wire.Shape(value, "featureKey", "reference", "variant", "value", "reason", "ruleId", "bucket", "experimentKey", "experimentVersion", "dependencies");
            string key = Wire.String(value["featureKey"]);
            var reference = Wire.Reference(value["reference"]);
            if (key != reference.Key)
                throw new InvalidConfigurationException("selection_key");
            string reason = Wire.String(value["reason"]);
            string? rule = Wire.OptionalString(value["ruleId"]), experimentKey = Wire.OptionalString(value["experimentKey"]), experimentVersion = Wire.OptionalString(value["experimentVersion"]);
            int? bucket = value["bucket"].Kind == JsonKind.Null ? null : checked((int)value["bucket"].AsInteger());
            if ((reason == "percentage" && (rule == null || bucket == null || bucket < 0 || bucket > 99 || experimentKey == null || experimentVersion == null)) || (reason != "percentage" && (bucket != null || experimentKey != null || experimentVersion != null)) || (reason == "rule" && rule == null) || ((reason == "default" || reason == "disabled") && rule != null) || (reason != "percentage" && reason != "rule" && reason != "default" && reason != "disabled"))
                throw new InvalidConfigurationException("selection_invalid");
            return new FeatureSelection(new Choice(reference.Revision, reference.SemanticSha, Wire.String(value["variant"]), value["value"], Wire.References(value["dependencies"], true)), reason, rule, bucket, experimentKey == null ? null : new ExperimentReference(experimentKey, experimentVersion!));
        }
        internal static FeatureOrigin Origin(JsonValue value)
        {
            Wire.Shape(value, "releaseSequence", "manifestRepresentationSha", "selectorRepresentationSha");
            return new FeatureOrigin(Sequence(value["releaseSequence"], true), Wire.Digest(value["manifestRepresentationSha"]), Wire.Digest(value["selectorRepresentationSha"]));
        }
        internal static void Activation(JsonValue value)
        {
            Wire.Shape(value, "selection", "origin", "closure", "durable");
            if (!value["durable"].AsBoolean())
                throw new InvalidConfigurationException("activation_not_durable");
            var selection = ReadSelection(value["selection"]);
            Origin(value["origin"]);
            var closure = Wire.References(value["closure"]);
            var root = new FeatureRevisionReference(Wire.String(value["selection"]["featureKey"]), selection.Choice.Revision, selection.Choice.SemanticSha);
            bool found = false;
            foreach (var item in closure)
                if (Wire.Same(item, root))
                    found = true;
            if (!found)
                throw new InvalidConfigurationException("closure_root_missing");
        }
        internal static CacheState Cache(byte[] bytes, CacheNamespace space)
        {
            var value = CanonicalJson.Parse(bytes);
            Wire.Shape(value, "schema", "app", "environment", "storeGeneration", "owner", "normal", "control", "objects", "activations", "pins");
            Wire.Scope(value, space.Scope, "platform.remote-config.cache-commit.v1");
            var state = new CacheState { Generation = Sequence(value["storeGeneration"]), Normal = value["normal"].Kind == JsonKind.Null ? null : value["normal"], Control = value["control"].Kind == JsonKind.Null ? null : value["control"] };
            if (space.Owner == null)
            {
                if (value["owner"].Kind != JsonKind.Null || value["activations"].AsArray().Count != 0 || value["pins"].AsArray().Count != 0)
                    throw new InvalidConfigurationException("owner_changed");
            }
            else
            {
                Wire.Shape(value["owner"], "ownerId", "subjectId", "generation");
                if (Wire.String(value["owner"]["ownerId"]) != space.Owner.OwnerId || Wire.String(value["owner"]["subjectId"], false) != space.Owner.SubjectId)
                    throw new InvalidConfigurationException("owner_changed");
                state.OwnerGeneration = Sequence(value["owner"]["generation"]);
            }
            foreach (var item in value["objects"].AsArray())
            {
                Wire.Shape(item, "representationSha", "decodedBytes", "lastUsedAtSeconds");
                string sha = Wire.Digest(item["representationSha"]);
                long length = item["decodedBytes"].AsInteger();
                if (length < 1 || length > ConfigurationLimits.Bytes || state.Objects.ContainsKey(sha))
                    throw new InvalidConfigurationException("object_invalid");
                state.Objects.Add(sha, new CachedObject((int)length, Wire.Time(item["lastUsedAtSeconds"])));
            }
            if (state.Objects.Count > 4096)
                throw new InvalidConfigurationException("cache_bounds");
            if (state.Normal != null)
            {
                Wire.Shape(state.Normal, "pointer", "pointerRepresentationSha", "manifestRepresentationSha", "selectorRepresentationSha", "etag", "time");
                var pointer = Pointer(CanonicalJson.Serialize(state.Normal["pointer"]), space.Scope);
                string pointerSha = Wire.Digest(state.Normal["pointerRepresentationSha"]), manifestSha = Wire.Digest(state.Normal["manifestRepresentationSha"]);
                Wire.Digest(state.Normal["selectorRepresentationSha"]);
                if (pointer.Manifest.RepresentationSha != manifestSha || !state.Objects.ContainsKey(pointerSha))
                    throw new InvalidConfigurationException("observation_object_missing");
                Observation(state.Normal);
            }
            if (state.Control != null)
            {
                Wire.Shape(state.Control, "document", "representationSha", "etag", "time");
                Control(CanonicalJson.Serialize(state.Control["document"]), space.Scope);
                string sha = Wire.Digest(state.Control["representationSha"]);
                if (!state.Objects.TryGetValue(sha, out var record) || record.Bytes > ConfigurationClientPolicy.ControlBytes)
                    throw new InvalidConfigurationException("observation_object_missing");
                Observation(state.Control);
            }
            foreach (var item in value["activations"].AsArray())
            {
                Activation(item);
                string key = Wire.String(item["selection"]["featureKey"]);
                if (state.Activations.ContainsKey(key))
                    throw new InvalidConfigurationException("activation_duplicate");
                state.Activations.Add(key, item);
            }
            foreach (var item in value["pins"].AsArray())
            {
                Wire.Shape(item, "pinId", "activation", "createdAtSeconds");
                string id = Wire.String(item["pinId"]);
                Wire.Uuid(id);
                Activation(item["activation"]);
                Wire.Time(item["createdAtSeconds"]);
                if (state.Pins.ContainsKey(id))
                    throw new InvalidConfigurationException("pin_duplicate");
                state.Pins.Add(id, item);
            }
            if (state.Activations.Count > 64 || state.Pins.Count > 4096)
                throw new InvalidConfigurationException("cache_bounds");
            return state;
        }
        private static void Observation(JsonValue value)
        {
            Wire.OptionalString(value["etag"]);
            Wire.Shape(value["time"], "observedAtSeconds", "anchorAtSeconds", "status");
            Wire.Time(value["time"]["observedAtSeconds"]);
            Wire.Time(value["time"]["anchorAtSeconds"]);
            string status = Wire.String(value["time"]["status"]);
            if (status != "trusted" && status != "unknown")
                throw new InvalidConfigurationException("anchor_invalid");
        }
        internal static byte[] CacheBytes(CacheState state, CacheNamespace space)
        {
            if (space.Owner != null && state.OwnerGeneration > space.Owner.Generation)
                throw new InvalidConfigurationException("owner_changed");
            var objects = new List<JsonValue>();
            var keys = new List<string>(state.Objects.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string sha in keys)
            {
                var item = state.Objects[sha];
                objects.Add(Wire.Object(("representationSha", JsonValue.String(sha)), ("decodedBytes", JsonValue.Integer(item.Bytes)), ("lastUsedAtSeconds", JsonValue.Integer(item.LastUsed))));
            }
            var activations = new List<JsonValue>();
            keys = new List<string>(state.Activations.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
                activations.Add(state.Activations[key]);
            var pins = new List<JsonValue>();
            keys = new List<string>(state.Pins.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
                pins.Add(state.Pins[key]);
            var owner = space.Owner == null ? JsonValue.Null : Wire.Object(("ownerId", JsonValue.String(space.Owner.OwnerId)), ("subjectId", JsonValue.String(space.Owner.SubjectId)), ("generation", Wire.Decimal(space.Owner.Generation)));
            byte[] bytes = CanonicalJson.Serialize(Wire.Object(("schema", JsonValue.String("platform.remote-config.cache-commit.v1")), ("app", JsonValue.String(space.Scope.App)), ("environment", JsonValue.String(space.Scope.Environment)), ("storeGeneration", Wire.Decimal(state.Generation)), ("owner", owner), ("normal", state.Normal ?? JsonValue.Null), ("control", state.Control ?? JsonValue.Null), ("objects", JsonValue.Array(objects)), ("activations", JsonValue.Array(activations)), ("pins", JsonValue.Array(pins))));
#if DEBUG
            Cache(bytes, space);
#endif
            return bytes;
        }
    }
}
