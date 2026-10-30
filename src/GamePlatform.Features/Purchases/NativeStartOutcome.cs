using System;
namespace GamePlatform.Features.Purchases
{
    public sealed class NativeStartOutcome
    {
        public NativeStartOutcome(Guid intentKey, NativeLaunchStatus status) { IntentKey = intentKey; Status = status; }
        public Guid IntentKey { get; }
        public NativeLaunchStatus Status { get; }
    }
}
