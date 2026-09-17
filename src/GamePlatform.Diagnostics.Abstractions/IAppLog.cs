using System;
using System.Collections.Generic;

namespace GamePlatform.Diagnostics.Abstractions
{
    public interface IAppLog
    {
        void Write(AppLogLevel level, string eventName, IReadOnlyDictionary<string, object?> fields, Exception? exception = null);
    }

    public enum AppLogLevel { Trace, Debug, Information, Warning, Error, Critical }

    public sealed class NullAppLog : IAppLog
    {
        public static NullAppLog Instance { get; } = new NullAppLog();
        private NullAppLog() { }
        public void Write(AppLogLevel level, string eventName, IReadOnlyDictionary<string, object?> fields, Exception? exception = null) { }
    }
}
