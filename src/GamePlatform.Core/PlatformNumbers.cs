using System;
using System.Security.Cryptography;

namespace GamePlatform.Core
{

    public static class PlatformNumbers
    {
        public const long MaximumUnixMilliseconds = 253402300799999;
        public static long UnixMilliseconds(long value)
        {
            if (value < 0 || value > MaximumUnixMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(value));
            return value;
        }
        public static long Add(long value, long delta) => checked(value + delta);
        public static long NextPositiveSequence(long current)
        {
            if (current < 0)
                throw new ArgumentOutOfRangeException(nameof(current));
            return checked(current + 1);
        }
    }
}
