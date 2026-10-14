using GamePlatform.Core;

namespace GamePlatform.Serialization.MessagePack
{
    public sealed class UnavailableMessagePackCodec
    {
        public byte[] Encode<T>(T value) => throw new PlatformCapabilityUnavailableException("messagepack-codec", "MessagePack-CSharp is selected but not implemented in M0.");
    }
}
