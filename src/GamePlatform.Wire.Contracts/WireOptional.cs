using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GamePlatform.Wire.Contracts
{
    /// <summary>Distinguishes an omitted field from an explicitly present null.</summary>
    public readonly struct WireOptional<T>
    {
        private readonly T value;
        private WireOptional(T value) { this.value = value; IsPresent = true; }
        public bool IsPresent { get; }
        public T Value => IsPresent ? value : throw new InvalidOperationException("wire_field_absent");
        public static WireOptional<T> Present(T value) => new WireOptional<T>(value);
        public static WireOptional<T> Absent => default;
    }

    internal static class WireCopy
    {
        internal static IReadOnlyList<T> List<T>(IReadOnlyList<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new T[source.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i];
            return Array.AsReadOnly(copy);
        }
        internal static IReadOnlyDictionary<string, T> Map<T>(IReadOnlyDictionary<string, T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var entry in source) copy.Add(entry.Key, entry.Value);
            return new ReadOnlyDictionary<string, T>(copy);
        }
    }

    public enum ExtensionValueKind { Null, Boolean, String, Array, Object }

    /// <summary>A recursively immutable extension tree. Numeric values are deliberately unsupported.</summary>
    public sealed class ExtensionValue
    {
        private ExtensionValue(ExtensionValueKind kind, bool boolean = false, string? text = null,
            IReadOnlyList<ExtensionValue>? items = null, IReadOnlyDictionary<string, ExtensionValue>? properties = null)
        { Kind = kind; Boolean = boolean; Text = text; Items = items; Properties = properties; }
        public ExtensionValueKind Kind { get; }
        public bool Boolean { get; }
        public string? Text { get; }
        public IReadOnlyList<ExtensionValue>? Items { get; }
        public IReadOnlyDictionary<string, ExtensionValue>? Properties { get; }
        public static ExtensionValue Null { get; } = new ExtensionValue(ExtensionValueKind.Null);
        public static ExtensionValue FromBoolean(bool value) => new ExtensionValue(ExtensionValueKind.Boolean, boolean: value);
        public static ExtensionValue FromString(string value) => new ExtensionValue(ExtensionValueKind.String, text: value ?? throw new ArgumentNullException(nameof(value)));
        public static ExtensionValue FromArray(IReadOnlyList<ExtensionValue> value) => new ExtensionValue(ExtensionValueKind.Array, items: WireCopy.List(value));
        public static ExtensionValue FromObject(IReadOnlyDictionary<string, ExtensionValue> value) => new ExtensionValue(ExtensionValueKind.Object, properties: WireCopy.Map(value));
    }

    public enum ErrorDetailKind { Null, Boolean, String, Integer }
    public readonly struct ErrorDetailValue
    {
        private ErrorDetailValue(ErrorDetailKind kind, bool boolean = false, string? text = null, int integer = 0)
        { Kind = kind; Boolean = boolean; Text = text; Integer = integer; }
        public ErrorDetailKind Kind { get; }
        public bool Boolean { get; }
        public string? Text { get; }
        public int Integer { get; }
        public static ErrorDetailValue Null => default;
        public static ErrorDetailValue FromBoolean(bool value) => new ErrorDetailValue(ErrorDetailKind.Boolean, boolean: value);
        public static ErrorDetailValue FromString(string value) => new ErrorDetailValue(ErrorDetailKind.String, text: value ?? throw new ArgumentNullException(nameof(value)));
        public static ErrorDetailValue FromInteger(int value) => new ErrorDetailValue(ErrorDetailKind.Integer, integer: value);
    }
}
