using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Backend.Contracts.Remote
{

    public enum RemoteFailureKind
    {
        Cancelled, NotSent, OutcomeUncertain, Authentication, Authorization, Validation, Conflict, RateLimited, Dependency, Protocol, Server, Unavailable, InspectionExpired
    }
}
