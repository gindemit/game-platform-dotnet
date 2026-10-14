using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Transport.Abstractions
{
    public interface IWireCodec
    {
        byte[] Encode<T>(T value); T Decode<T>(byte[] payload);
    }
}
