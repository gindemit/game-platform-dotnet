namespace GamePlatform.Diagnostics.Abstractions
{
    /// <summary>A synchronous, borrowed sink. Implementations must not block or retain an unbounded queue.</summary>
    public interface IStructuredAppLogSink
    {
        bool IsEnabled(AppLogLevel level, string category);
        void Write(AppLogRecord record);
    }
}
