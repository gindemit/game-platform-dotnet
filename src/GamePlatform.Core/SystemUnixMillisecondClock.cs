using System;
using System.Security.Cryptography;

namespace GamePlatform.Core
{

    public sealed class SystemUnixMillisecondClock : IUnixMillisecondClock
    {
        public long GetUnixMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
