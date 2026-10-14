namespace GamePlatform.Diagnostics.Abstractions
{

    public sealed class NullStructuredAppLogSink : IStructuredAppLogSink
    {
        public bool IsEnabled(AppLogLevel level, string category) => false;
        public void Write(AppLogRecord record)
        {
        }
    }
}
