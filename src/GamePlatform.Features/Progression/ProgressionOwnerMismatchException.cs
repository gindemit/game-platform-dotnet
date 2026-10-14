using System;

namespace GamePlatform.Features.Progression
{
    public sealed class ProgressionOwnerMismatchException : InvalidOperationException
    {
        public ProgressionOwnerMismatchException() : base("The progression request does not belong to the captured account/view generation.") { }
    }
}
