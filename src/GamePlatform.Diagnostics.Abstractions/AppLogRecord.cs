using System.Collections.Generic;

namespace GamePlatform.Diagnostics.Abstractions
{

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
}
