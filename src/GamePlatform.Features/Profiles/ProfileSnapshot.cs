using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{

    /// <summary>Immutable observation combining the last confirmed projection and (if present) a local intent.</summary>
    public sealed class ProfileSnapshot
    {
        public ProfileSnapshot(ProfileConfirmed? confirmed, PendingProfileEdit? pending)
        {
            Confirmed = confirmed;
            Pending = pending;
        }
        public ProfileConfirmed? Confirmed { get; }
        public PendingProfileEdit? Pending { get; }
    }
}
