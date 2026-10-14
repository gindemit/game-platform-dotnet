using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{

    public sealed class ProfileOwnerMismatchException : InvalidOperationException
    {
        public ProfileOwnerMismatchException() : base("The profile result does not belong to the captured account/view generation.") { }
    }
}
