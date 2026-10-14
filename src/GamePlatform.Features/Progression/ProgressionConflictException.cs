using System;

namespace GamePlatform.Features.Progression
{

    public sealed class ProgressionConflictException : InvalidOperationException
    {
        public ProgressionConflictException(string message) : base(message) { }
    }
}
