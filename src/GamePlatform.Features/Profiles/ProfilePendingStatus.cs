using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{

    public enum ProfilePendingStatus
    {
        AwaitingReceipt = 0, AcceptedAwaitingPull = 1, Conflict = 2
    }
}
