using System;
namespace GamePlatform.Features.Purchases
{
    public sealed class NativeRecoveryException : Exception
    {
        public NativeRecoveryException(int unresolvedCount) : base("Some native purchases remain unresolved.") { UnresolvedCount = unresolvedCount; }
        public int UnresolvedCount { get; }
    }
}
