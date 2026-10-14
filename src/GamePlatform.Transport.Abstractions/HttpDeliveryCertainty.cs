using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Transport.Abstractions
{
    public enum HttpDeliveryCertainty
    {
        NotSent, Uncertain, ResponseReceived
    }
}
