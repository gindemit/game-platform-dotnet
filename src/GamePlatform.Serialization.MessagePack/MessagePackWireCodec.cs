using System;
using GamePlatform.Transport.Abstractions;

namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>
    /// Production transport adapter for the reviewed core wire DTO catalog.
    /// Desktop qualification does not imply Unity AOT, stripping, device
    /// packaging or concrete HTTP-host acceptance.
    /// </summary>
    public sealed class MessagePackWireCodec : IWireCodec
    {
        private readonly TypedQualificationCodec typedCodec;

        public MessagePackWireCodec()
            : this(new TypedQualificationCodec())
        {
        }

        internal MessagePackWireCodec(TypedQualificationCodec typedCodec)
        {
            this.typedCodec = typedCodec ?? throw new ArgumentNullException(nameof(typedCodec));
        }

        public byte[] Encode<T>(T value) => typedCodec.Encode(value);

        public T Decode<T>(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            return typedCodec.Decode<T>(payload);
        }
    }
}
