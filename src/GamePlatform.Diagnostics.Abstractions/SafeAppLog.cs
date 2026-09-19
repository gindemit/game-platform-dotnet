using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GamePlatform.Diagnostics.Abstractions
{
    /// <summary>A synchronous, borrowed sink. Implementations must not block or retain an unbounded queue.</summary>
    public interface IStructuredAppLogSink
    {
        bool IsEnabled(AppLogLevel level, string category);
        void Write(AppLogRecord record);
    }

    /// <summary>Process-local correlation, captured when a scope is constructed. Never supply a provider/user ID.</summary>
    public sealed class AppLogContext
    {
        public AppLogContext(Guid correlationId, long generation)
        {
            if (correlationId == Guid.Empty) throw new ArgumentException("Correlation must be nonempty.", nameof(correlationId));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            CorrelationId = correlationId;
            Generation = generation;
        }

        public Guid CorrelationId { get; }
        public long Generation { get; }
    }

    /// <summary>Immutable sanitized data. Exceptions, arbitrary strings and mutable objects never cross this boundary.</summary>
    public sealed class AppLogRecord
    {
        internal AppLogRecord(AppLogLevel level, string category, string eventName, AppLogContext context,
            IReadOnlyDictionary<string, object?> fields, bool hasException)
        {
            Level = level;
            Category = category;
            EventName = eventName;
            Context = context;
            Fields = fields;
            HasException = hasException;
        }

        public AppLogLevel Level { get; }
        public string Category { get; }
        public string EventName { get; }
        public AppLogContext Context { get; }
        public IReadOnlyDictionary<string, object?> Fields { get; }
        public bool HasException { get; }
    }

    public sealed class NullStructuredAppLogSink : IStructuredAppLogSink
    {
        public bool IsEnabled(AppLogLevel level, string category) => false;
        public void Write(AppLogRecord record) { }
    }

    /// <summary>Best-effort diagnostics. Sink, enumeration and factory failures are contained and never affect business results.</summary>
    public sealed class SafeAppLog : IAppLog
    {
        public const int MaximumFields = 16;
        public const int MaximumSymbolLength = 64;
        public const string Redacted = "[redacted]";
        private readonly IStructuredAppLogSink sink;
        private readonly AppLogContext context;

        public SafeAppLog(IStructuredAppLogSink sink, AppLogContext context)
        {
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public bool IsEnabled(AppLogLevel level, string category)
        {
            try { return ValidLevel(level) && ValidSymbol(category) && sink.IsEnabled(level, category); }
            catch (Exception) { return false; }
        }

        /// <summary>Names must be code-defined symbols, never user text. All string field values are redacted by default.</summary>
        public void Write(AppLogLevel level, string category, string eventName,
            Func<IEnumerable<KeyValuePair<string, object?>>> fieldsFactory, Exception? exception = null)
        {
            if (!ValidSymbol(eventName) || !IsEnabled(level, category)) return;
            try
            {
                var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
                // At most MaximumFields source entries are inspected, including invalid and duplicate names.
                using (var iterator = fieldsFactory().GetEnumerator())
                {
                    for (var i = 0; i < MaximumFields && iterator.MoveNext(); i++)
                    {
                        var field = iterator.Current;
                        if (!ValidSymbol(field.Key)) continue;
                        fields[field.Key] = Metric(field.Key) ? SafeValue(field.Value) : Redacted;
                    }
                }
                sink.Write(new AppLogRecord(level, category, eventName, context,
                    new ReadOnlyDictionary<string, object?>(fields), exception != null));
            }
            catch (Exception) { /* Diagnostics cannot determine transaction success. No recursive logging. */ }
        }

        // Source-compatible legacy entry point. New callers use the deferred overload above.
        public void Write(AppLogLevel level, string eventName, IReadOnlyDictionary<string, object?> fields, Exception? exception = null)
            => Write(level, "Legacy", eventName, () => fields, exception);

        private static bool ValidLevel(AppLogLevel level) => level >= AppLogLevel.Trace && level <= AppLogLevel.Critical;

        private static bool ValidSymbol(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaximumSymbolLength) return false;
            foreach (var c in value)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-'))
                    return false;
            return true;
        }

        private static bool Metric(string name)
        {
            // Unknown scalar names are private by default too; a secret need not be a string.
            switch (name)
            {
                case "count": case "attempt": case "attemptCount": case "durationMs": case "elapsedMs":
                case "bytes": case "byteCount": case "queueDepth": case "statusCode": case "errorCode":
                case "retryable": case "ok": case "revision": case "sequence": case "nothing":
                    return true;
                default: return false;
            }
        }

        private static object? SafeValue(object? value)
        {
            // Do not call arbitrary ToString(), serialize nested data, or keep mutable references.
            if (value == null || value is bool || value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong || value is decimal) return value;
            if (value is double d && !double.IsNaN(d) && !double.IsInfinity(d)) return d;
            if (value is float f && !float.IsNaN(f) && !float.IsInfinity(f)) return f;
            return Redacted;
        }
    }
}
