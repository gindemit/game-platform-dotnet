using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Navigation
{
    public enum NavigationOutcome
    {
        Applied, Cancelled, Stale, Unavailable
    }
}
