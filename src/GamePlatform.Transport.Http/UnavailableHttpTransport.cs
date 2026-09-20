using GamePlatform.Core;

namespace GamePlatform.Transport.Http
{
    public sealed class UnavailableHttpTransport
    {
        public void EnsureAvailable() => throw new PlatformCapabilityUnavailableException("http-transport", "No production HTTP provider composition was supplied by the application host.");
    }
}
