using System;
using GamePlatform.Transport.Abstractions;

namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>
    /// Production wire adapter for the reviewed DTO catalog. The underlying
    /// generated mapping and bounded MessagePack reader/writer reject every
    /// unreviewed type and representation.
    /// </summary>
    public sealed class MessagePackWireCodec : IWireCodec
    {
        private readonly TypedQualificationCodec codec;

        public MessagePackWireCodec()
            : this(new TypedQualificationCodec())
        {
        }

        internal MessagePackWireCodec(TypedQualificationCodec codec)
        {
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        public byte[] Encode<T>(T value) => codec.Encode(value);

        public T Decode<T>(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            return codec.Decode<T>(payload);
        }
    }
}
