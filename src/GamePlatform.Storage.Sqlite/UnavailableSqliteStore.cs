using GamePlatform.Core;

namespace GamePlatform.Storage.Sqlite
{
    public sealed class UnavailableSqliteStore
    {
        public void Open() => throw new PlatformCapabilityUnavailableException("sqlite-storage", "The selected Unity native driver is not installed or validated in M0.");
    }
}
