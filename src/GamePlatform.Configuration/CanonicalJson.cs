using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GamePlatform.Configuration
{
    public static class CanonicalJson
    {
        public static JsonValue Parse(byte[] utf8)
        {
            if (utf8 == null)
                throw new ArgumentNullException(nameof(utf8));
            if (utf8.Length > ConfigurationLimits.Bytes)
                throw new InvalidConfigurationException("byte_limit");
            string text;
            try
            {
                text = ConfigurationLimits.Utf8.GetString(utf8);
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidConfigurationException("utf8_invalid");
            }
            return new Reader(text).Read();
        }

        public static byte[] Serialize(JsonValue value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var builder = new StringBuilder();
            int nodes = 0;
            Write(value, builder, 1, ref nodes);
            byte[] bytes = ConfigurationLimits.Utf8.GetBytes(builder.ToString());
            if (bytes.Length > ConfigurationLimits.Bytes)
                throw new InvalidConfigurationException("byte_limit");
            return bytes;
        }

        public static string SemanticSha256(JsonValue value) => Sha256(Serialize(value));
        public static string Sha256(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            using (var algorithm = SHA256.Create())
                return Hex(algorithm.ComputeHash(bytes));
        }
        internal static string Hex(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                text.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return text.ToString();
        }

        private static void Write(JsonValue value, StringBuilder text, int depth, ref int nodes)
        {
            if (depth > ConfigurationLimits.Depth)
                throw new InvalidConfigurationException("depth_limit");
            if (++nodes > ConfigurationLimits.Nodes)
                throw new InvalidConfigurationException("node_limit");
            switch (value.Kind)
            {
                case JsonKind.Null:
                    text.Append("null");
                    break;
                case JsonKind.Boolean:
                    text.Append(value.AsBoolean() ? "true" : "false");
                    break;
                case JsonKind.Integer:
                    text.Append(value.AsInteger().ToString(CultureInfo.InvariantCulture));
                    break;
                case JsonKind.String:
                    WriteString(value.AsString(), text);
                    break;
                case JsonKind.Array:
                    text.Append('[');
                    bool first = true;
                    foreach (var item in value.AsArray())
                    {
                        if (!first)
                            text.Append(',');
                        first = false;
                        Write(item, text, depth + 1, ref nodes);
                    }
                    text.Append(']');
                    break;
                case JsonKind.Object:
                    text.Append('{');
                    var keys = new List<string>(value.AsObject().Keys);
                    keys.Sort(StringComparer.Ordinal);
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (i > 0)
                            text.Append(',');
                        WriteString(keys[i], text);
                        text.Append(':');
                        Write(value[keys[i]], text, depth + 1, ref nodes);
                    }
                    text.Append('}');
                    break;
                default:
                    throw new InvalidConfigurationException("kind_invalid");
            }
            // Bound output while visiting instead of materializing unbounded graphs.
            if (text.Length > ConfigurationLimits.Bytes)
                throw new InvalidConfigurationException("byte_limit");
        }

        private static void WriteString(string value, StringBuilder text)
        {
            text.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        text.Append("\\\"");
                        break;
                    case '\\':
                        text.Append("\\\\");
                        break;
                    case '\b':
                        text.Append("\\b");
                        break;
                    case '\f':
                        text.Append("\\f");
                        break;
                    case '\n':
                        text.Append("\\n");
                        break;
                    case '\r':
                        text.Append("\\r");
                        break;
                    case '\t':
                        text.Append("\\t");
                        break;
                    default:
                        if (c < 32)
                            text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            text.Append(c);
                        break;
                }
            }
            text.Append('"');
        }

        private sealed class Reader
        {
            private readonly string text;
            private int offset;
            private int nodes;
            internal Reader(string text)
            {
                this.text = text;
            }
            internal JsonValue Read()
            {
                JsonValue value = Value(1);
                Space();
                if (offset != text.Length)
                    throw new InvalidConfigurationException("trailing_input");
                return value;
            }
            private void Space()
            {
                while (offset < text.Length && (text[offset] == ' ' || text[offset] == '\t' || text[offset] == '\n' || text[offset] == '\r'))
                    offset++;
            }
            private char Take() => offset < text.Length ? text[offset++] : throw new InvalidConfigurationException("truncated_input");
            private bool Consume(char c)
            {
                Space();
                if (offset >= text.Length || text[offset] != c)
                    return false;
                offset++;
                return true;
            }
            private void Expect(char c)
            {
                if (!Consume(c))
                    throw new InvalidConfigurationException("token_expected");
            }
            private JsonValue Value(int depth)
            {
                if (depth > ConfigurationLimits.Depth)
                    throw new InvalidConfigurationException("depth_limit");
                if (++nodes > ConfigurationLimits.Nodes)
                    throw new InvalidConfigurationException("node_limit");
                Space();
                if (offset >= text.Length)
                    throw new InvalidConfigurationException("truncated_input");
                char c = text[offset];
                if (c == '"')
                    return JsonValue.String(String());
                if (c == '{')
                {
                    offset++;
                    var values = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                    if (!Consume('}'))
                    {
                        do
                        {
                            Space();
                            string key = String();
                            ConfigurationLimits.Key(key);
                            if (values.ContainsKey(key))
                                throw new InvalidConfigurationException("duplicate_key");
                            Expect(':');
                            values.Add(key, Value(depth + 1));
                        } while (Consume(','));
                        Expect('}');
                    }
                    return JsonValue.Object(values);
                }
                if (c == '[')
                {
                    offset++;
                    var values = new List<JsonValue>();
                    if (!Consume(']'))
                    {
                        do
                        {
                            values.Add(Value(depth + 1));
                        } while (Consume(','));
                        Expect(']');
                    }
                    return JsonValue.Array(values);
                }
                if (c == 'n')
                {
                    Literal("null");
                    return JsonValue.Null;
                }
                if (c == 't')
                {
                    Literal("true");
                    return JsonValue.Boolean(true);
                }
                if (c == 'f')
                {
                    Literal("false");
                    return JsonValue.Boolean(false);
                }
                int start = offset;
                if (c == '-')
                    offset++;
                if (offset >= text.Length || text[offset] < '0' || text[offset] > '9')
                    throw new InvalidConfigurationException("number_invalid");
                if (text[offset] == '0')
                {
                    offset++;
                    if (text[start] == '-' || (offset < text.Length && text[offset] >= '0' && text[offset] <= '9'))
                        throw new InvalidConfigurationException("number_invalid");
                }
                else
                    while (offset < text.Length && text[offset] >= '0' && text[offset] <= '9')
                        offset++;
                if (offset < text.Length && (text[offset] == '.' || text[offset] == 'e' || text[offset] == 'E'))
                    throw new InvalidConfigurationException("number_invalid");
                if (!long.TryParse(text.Substring(start, offset - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number))
                    throw new InvalidConfigurationException("integer_range");
                return JsonValue.Integer(number);
            }
            private void Literal(string token)
            {
                foreach (char c in token)
                    if (Take() != c)
                        throw new InvalidConfigurationException("literal_invalid");
            }
            private string String()
            {
                if (Take() != '"')
                    throw new InvalidConfigurationException("string_expected");
                var value = new StringBuilder();
                while (true)
                {
                    char c = Take();
                    if (c == '"')
                        break;
                    if (c < 32)
                        throw new InvalidConfigurationException("string_control");
                    if (c == '\\')
                    {
                        char escape = Take();
                        switch (escape)
                        {
                            case '"':
                                c = '"';
                                break;
                            case '\\':
                                c = '\\';
                                break;
                            case '/':
                                c = '/';
                                break;
                            case 'b':
                                c = '\b';
                                break;
                            case 'f':
                                c = '\f';
                                break;
                            case 'n':
                                c = '\n';
                                break;
                            case 'r':
                                c = '\r';
                                break;
                            case 't':
                                c = '\t';
                                break;
                            case 'u':
                                c = Unicode();
                                break;
                            default:
                                throw new InvalidConfigurationException("escape_invalid");
                        }
                    }
                    value.Append(c);
                    if (value.Length > ConfigurationLimits.StringBytes)
                        throw new InvalidConfigurationException("string_limit");
                }
                string result = value.ToString();
                ConfigurationLimits.String(result);
                return result;
            }
            private char Unicode()
            {
                int number = 0;
                for (int i = 0; i < 4; i++)
                {
                    char c = Take();
                    int digit = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
                    if (digit < 0)
                        throw new InvalidConfigurationException("escape_invalid");
                    number = number * 16 + digit;
                }
                return (char)number;
            }
        }
    }
}
