using System;
namespace GamePlatform.Features.Purchases
{
    public sealed class NativeAttempt
    {
        public NativeAttempt(Guid intentKey, NativeProduct product, NativeIntentState? intent, bool launchRequested)
        { IntentKey = intentKey; Product = product; Intent = intent; LaunchRequested = launchRequested; }
        public Guid IntentKey { get; }
        public NativeProduct Product { get; }
        public NativeIntentState? Intent { get; }
        public bool LaunchRequested { get; }
    }
}
