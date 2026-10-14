using GamePlatform.Core;

namespace GamePlatform.Serialization.Json
{
    public sealed class UnavailableJsonCodec
    {
        public byte[] Encode<T>(T value) => throw new PlatformCapabilityUnavailableException("json-codec", "Diagnostic JSON codec selection is pending target validation.");
    }
}
