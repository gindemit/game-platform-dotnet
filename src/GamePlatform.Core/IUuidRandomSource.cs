using System;
using System.Security.Cryptography;

namespace GamePlatform.Core
{
    public interface IUuidRandomSource
    {
        void Fill(Span<byte> destination);
    }
}
