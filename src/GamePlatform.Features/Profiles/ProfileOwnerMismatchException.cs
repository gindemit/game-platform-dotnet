using System;

namespace GamePlatform.Features.Profiles
{

    public sealed class ProfileOwnerMismatchException : InvalidOperationException
    {
        public ProfileOwnerMismatchException() : base("The profile result does not belong to the captured account/view generation.") { }
    }
}
