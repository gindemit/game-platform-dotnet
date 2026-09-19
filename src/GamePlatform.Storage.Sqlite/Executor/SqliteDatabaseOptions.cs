using System;

namespace GamePlatform.Storage.Sqlite.Executor
{
    public sealed class SqliteDatabaseOptions
    {
        public SqliteDatabaseOptions(TimeSpan busyTimeout, Func<long> nowMilliseconds)
        {
            if (busyTimeout < TimeSpan.FromMilliseconds(1) || busyTimeout > TimeSpan.FromSeconds(30))
                throw new ArgumentOutOfRangeException(nameof(busyTimeout));
            BusyTimeout = busyTimeout;
            NowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
        }

        public TimeSpan BusyTimeout { get; }
        internal Func<long> NowMilliseconds { get; }

        public static SqliteDatabaseOptions Default { get; } = new SqliteDatabaseOptions(
            TimeSpan.FromSeconds(2),
            () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
}
