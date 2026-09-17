using GamePlatform.Core;

namespace GamePlatform.Transport.Http
{
    public sealed class UnavailableHttpTransport
    {
        public void EnsureAvailable() => throw new PlatformCapabilityUnavailableException("http-transport", "No executor or authenticated endpoint is configured in M0.");
    }
}
