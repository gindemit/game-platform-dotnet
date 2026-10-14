using System;
using System.Security.Cryptography;

namespace GamePlatform.Core
{

    public interface IUnixMillisecondClock
    {
        long GetUnixMilliseconds();
    }
}
