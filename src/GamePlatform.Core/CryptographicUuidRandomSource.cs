using System;
using System.Security.Cryptography;

namespace GamePlatform.Core
{

    public sealed class CryptographicUuidRandomSource : IUuidRandomSource
    {
        public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
    }
}
