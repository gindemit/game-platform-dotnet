using System;

namespace GamePlatform.Core
{
    public interface IUuidRandomSource
    {
        void Fill(Span<byte> destination);
    }
}
