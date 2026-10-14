using System;

namespace GamePlatform.Features.Profiles
{

    public sealed class ProfileConflictException : InvalidOperationException
    {
        public ProfileConflictException(string message) : base(message) { }
    }
}
