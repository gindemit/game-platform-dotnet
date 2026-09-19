using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace GamePlatform.Serialization.MessagePack
{
    public enum QualificationValueKind { Null, Boolean, Integer, String, Binary, Array, Object }

    /// <summary>Immutable semantic value for explicit desktop protocol qualification. Not a production codec contract.</summary>
    public sealed class QualificationValue
    {
        internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly object? value;
        public QualificationValueKind Kind { get; }
        internal long EncodedByteLength { get; set; }
        private QualificationValue(QualificationValueKind kind, object? value) { Kind = kind; this.value = value; }
        public static QualificationValue Null { get; } = new QualificationValue(QualificationValueKind.Null, null);
        public static QualificationValue Boolean(bool value) => new QualificationValue(QualificationValueKind.Boolean, value);
        public static QualificationValue Integer(long value) => new QualificationValue(QualificationValueKind.Integer, value);
        public static QualificationValue String(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            Utf8.GetByteCount(value);
            return new QualificationValue(QualificationValueKind.String, value);
        }
        public static QualificationValue Binary(byte[] value) => new QualificationValue(QualificationValueKind.Binary, (byte[])(value ?? throw new ArgumentNullException(nameof(value))).Clone());
        public static QualificationValue Array(IEnumerable<QualificationValue> values)
        {
            var copy = (values ?? throw new ArgumentNullException(nameof(values))).ToArray();
            if (copy.Any(v => v == null)) throw new ArgumentException("Null value reference.");
            return new QualificationValue(QualificationValueKind.Array, System.Array.AsReadOnly(copy));
        }
        public static QualificationValue Object(IEnumerable<KeyValuePair<string, QualificationValue>> values)
        {
            var copy = new Dictionary<string, QualificationValue>(StringComparer.Ordinal);
            foreach (var pair in values ?? throw new ArgumentNullException(nameof(values)))
            {
                Utf8.GetByteCount(pair.Key);
                if (pair.Value == null || !copy.TryAdd(pair.Key, pair.Value)) throw new ArgumentException("Null value or duplicate key.");
            }
            return new QualificationValue(QualificationValueKind.Object, new ReadOnlyDictionary<string, QualificationValue>(copy));
        }
        public bool BooleanValue => Kind == QualificationValueKind.Boolean ? (bool)value! : throw new InvalidOperationException();
        public long IntegerValue => Kind == QualificationValueKind.Integer ? (long)value! : throw new InvalidOperationException();
        public string StringValue => Kind == QualificationValueKind.String ? (string)value! : throw new InvalidOperationException();
        public byte[] BinaryValue => (byte[])BinaryBytes.Clone();
        internal byte[] BinaryBytes => Kind == QualificationValueKind.Binary ? (byte[])value! : throw new InvalidOperationException();
        internal static QualificationValue OwnedBinary(byte[] bytes) => new QualificationValue(QualificationValueKind.Binary,bytes);
        internal static QualificationValue OwnedArray(QualificationValue[] items) => new QualificationValue(QualificationValueKind.Array,System.Array.AsReadOnly(items));
        internal static QualificationValue OwnedObject(Dictionary<string,QualificationValue> properties) => new QualificationValue(QualificationValueKind.Object,new ReadOnlyDictionary<string,QualificationValue>(properties));
        public IReadOnlyList<QualificationValue> Items => Kind == QualificationValueKind.Array ? (IReadOnlyList<QualificationValue>)value! : throw new InvalidOperationException();
        public IReadOnlyDictionary<string, QualificationValue> Properties => Kind == QualificationValueKind.Object ? (IReadOnlyDictionary<string, QualificationValue>)value! : throw new InvalidOperationException();
    }

    public sealed class QualificationCodecException : FormatException
    {
        public QualificationCodecException(string message) : base(message) { }
        public QualificationCodecException(string message, Exception inner) : base(message, inner) { }
    }
}
