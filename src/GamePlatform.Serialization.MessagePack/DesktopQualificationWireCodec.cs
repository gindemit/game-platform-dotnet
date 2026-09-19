using System;

namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>
    /// Desktop-only qualification candidate for the reviewed core wire DTO
    /// catalog. It deliberately does not implement the production IWireCodec
    /// port; Unity AOT, stripping, device packaging and host acceptance remain
    /// required before that port can be made available.
    /// </summary>
    internal sealed class DesktopQualificationWireCodec
    {
        private readonly TypedQualificationCodec typedCodec;

        public DesktopQualificationWireCodec()
            : this(new TypedQualificationCodec())
        {
        }

        internal DesktopQualificationWireCodec(TypedQualificationCodec typedCodec)
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
