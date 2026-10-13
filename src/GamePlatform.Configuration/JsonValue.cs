using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace GamePlatform.Configuration
{
    public sealed class InvalidConfigurationException : Exception
    {
        public InvalidConfigurationException(string code) : base(code) { }
    }

    public static class ConfigurationLimits
    {
        public const int Bytes = 1048576;
        public const int Depth = 32;
        public const int Nodes = 10000;
        public const int StringBytes = 4096;
        public const int Rules = 1000;
        public const long SafeInteger = 9007199254740991;
        internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static void String(string value, bool nonempty = false)
        {
            if (value == null || (nonempty && value.Length == 0)) throw new InvalidConfigurationException("string_required");
            try
            {
                if (Utf8.GetByteCount(value) > StringBytes) throw new InvalidConfigurationException("string_limit");
            }
            catch (EncoderFallbackException) { throw new InvalidConfigurationException("unicode_invalid"); }
        }

        internal static void Key(string key)
        {
            String(key, true);
            if (key == "__proto__" || key == "prototype" || key == "constructor") throw new InvalidConfigurationException("key_forbidden");
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || c == '.' || c == '-' || (i > 0 && c >= '0' && c <= '9')))
                    throw new InvalidConfigurationException("key_invalid");
            }
        }

        internal static void Digest(string digest)
        {
            if (digest == null || digest.Length != 64) throw new InvalidConfigurationException("digest_invalid");
            foreach (char c in digest) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) throw new InvalidConfigurationException("digest_invalid");
        }
    }

    public enum JsonKind { Null, Boolean, Integer, String, Array, Object }

    /// <summary>Immutable restricted JSON value; no floating point or runtime type payloads.</summary>
    public sealed class JsonValue
    {
        private readonly object? payload;
        public JsonKind Kind { get; }
        private JsonValue(JsonKind kind, object? payload) { Kind = kind; this.payload = payload; }
        public static JsonValue Null { get; } = new JsonValue(JsonKind.Null, null);
        public static JsonValue Boolean(bool value) => new JsonValue(JsonKind.Boolean, value);
        public static JsonValue Integer(long value)
        {
            if (value < -ConfigurationLimits.SafeInteger || value > ConfigurationLimits.SafeInteger) throw new InvalidConfigurationException("integer_range");
            return new JsonValue(JsonKind.Integer, value);
        }
        public static JsonValue String(string value) { ConfigurationLimits.String(value); return new JsonValue(JsonKind.String, value); }
        public static JsonValue Array(IEnumerable<JsonValue> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var copy = new List<JsonValue>();
            foreach (var value in values)
            {
                if (value == null) throw new InvalidConfigurationException("null_node");
                if (copy.Count >= ConfigurationLimits.Nodes) throw new InvalidConfigurationException("node_limit");
                copy.Add(value);
            }
            return new JsonValue(JsonKind.Array, copy.AsReadOnly());
        }
        public static JsonValue Object(IEnumerable<KeyValuePair<string, JsonValue>> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var copy = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            foreach (var pair in values)
            {
                ConfigurationLimits.Key(pair.Key);
                if (pair.Value == null) throw new InvalidConfigurationException("null_node");
                if (copy.ContainsKey(pair.Key)) throw new InvalidConfigurationException("duplicate_key");
                if (copy.Count >= ConfigurationLimits.Nodes) throw new InvalidConfigurationException("node_limit");
                copy.Add(pair.Key, pair.Value);
            }
            return new JsonValue(JsonKind.Object, new ReadOnlyDictionary<string, JsonValue>(copy));
        }
        public bool AsBoolean() => Kind == JsonKind.Boolean ? (bool)payload! : throw new InvalidConfigurationException("boolean_required");
        public long AsInteger() => Kind == JsonKind.Integer ? (long)payload! : throw new InvalidConfigurationException("integer_required");
        public string AsString() => Kind == JsonKind.String ? (string)payload! : throw new InvalidConfigurationException("string_required");
        public IReadOnlyList<JsonValue> AsArray() => Kind == JsonKind.Array ? (IReadOnlyList<JsonValue>)payload! : throw new InvalidConfigurationException("array_required");
        public IReadOnlyDictionary<string, JsonValue> AsObject() => Kind == JsonKind.Object ? (IReadOnlyDictionary<string, JsonValue>)payload! : throw new InvalidConfigurationException("object_required");
        public JsonValue this[string key] => AsObject().TryGetValue(key, out var value) ? value : throw new InvalidConfigurationException("field_missing:" + key);
    }
}
