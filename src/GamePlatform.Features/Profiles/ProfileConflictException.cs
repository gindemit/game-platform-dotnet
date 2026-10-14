using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Profiles
{

    public sealed class ProfileConflictException : InvalidOperationException
    {
        public ProfileConflictException(string message) : base(message) { }
    }
}
