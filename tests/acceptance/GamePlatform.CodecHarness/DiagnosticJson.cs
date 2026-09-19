using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GamePlatform.Serialization.MessagePack;

namespace GamePlatform.CodecHarness;

// Test-host parser only. It does not enable JSON in the portable production adapter.
internal static class DiagnosticJson
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static QualificationValue Parse(byte[] bytes)
    {
        if (bytes.Length > 262144) throw new InvalidDataException("diagnostic_input_limit");
        _ = StrictUtf8.GetString(bytes);
        using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        int nodes = 0;
        return Read(doc.RootElement, ref nodes);
    }

    internal static QualificationValue Read(JsonElement value)
    {
        int nodes = 0;
        return Read(value, ref nodes);
    }

    private static QualificationValue Read(JsonElement value, ref int nodes)
    {
        if (++nodes > 16384) throw new InvalidDataException("diagnostic_nodes_limit");
        switch (value.ValueKind)
        {
            case JsonValueKind.Null: return QualificationValue.Null;
            case JsonValueKind.True: return QualificationValue.Boolean(true);
            case JsonValueKind.False: return QualificationValue.Boolean(false);
            case JsonValueKind.String: return QualificationValue.String(Text(value.GetString()!));
            case JsonValueKind.Number:
                string raw = value.GetRawText();
                if (raw == "-0" || raw.IndexOfAny(new[] { '.', 'e', 'E', '+' }) >= 0 ||
                    !long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer))
                    throw new InvalidDataException("diagnostic_integer_lexical_form");
                return QualificationValue.Integer(integer);
            case JsonValueKind.Array:
                if (value.GetArrayLength() > 1024) throw new InvalidDataException("diagnostic_array_limit");
                var items = new List<QualificationValue>();
                foreach (var item in value.EnumerateArray()) items.Add(Read(item, ref nodes));
                return QualificationValue.Array(items);
            case JsonValueKind.Object:
                var fields = new List<KeyValuePair<string, QualificationValue>>();
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var field in value.EnumerateObject())
                {
                    string key = Text(field.Name);
                    if (!keys.Add(key)) throw new InvalidDataException("diagnostic_duplicate_key");
                    if (keys.Count > 256 || ++nodes > 16384) throw new InvalidDataException("diagnostic_map_limit");
                    fields.Add(new(key, Read(field.Value, ref nodes)));
                }
                return QualificationValue.Object(fields);
            default: throw new InvalidDataException("diagnostic_kind");
        }
    }

    private static string Text(string text)
    {
        if (StrictUtf8.GetByteCount(text) > 8192) throw new InvalidDataException("diagnostic_string_limit");
        return text;
    }

    internal static byte[] Write(QualificationValue value)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output)) Write(writer, value);
        return output.ToArray();
    }

    private static void Write(Utf8JsonWriter writer, QualificationValue value)
    {
        switch (value.Kind.ToString())
        {
            case "Null": writer.WriteNullValue(); break;
            case "Boolean": writer.WriteBooleanValue(value.BooleanValue); break;
            case "Integer": writer.WriteNumberValue(value.IntegerValue); break;
            case "String": writer.WriteStringValue(value.StringValue); break;
            case "Array":
                writer.WriteStartArray();
                foreach (var item in value.Items) Write(writer, item);
                writer.WriteEndArray(); break;
            case "Object":
                writer.WriteStartObject();
                foreach (var field in value.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
                { writer.WritePropertyName(field.Key); Write(writer, field.Value); }
                writer.WriteEndObject(); break;
            default: throw new InvalidDataException("binary_not_diagnostic");
        }
    }
}
