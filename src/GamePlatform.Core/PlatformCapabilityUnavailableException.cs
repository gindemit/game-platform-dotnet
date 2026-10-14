using System;

namespace GamePlatform.Core
{

    public sealed class PlatformCapabilityUnavailableException : NotSupportedException
    {
        public PlatformCapabilityUnavailableException(string capability, string reason)
            : base($"Capability '{capability}' is unavailable: {reason}")
        {
            Capability = capability;
            Reason = reason;
        }

        public string Capability { get; }
        public string Reason { get; }
    }
}
