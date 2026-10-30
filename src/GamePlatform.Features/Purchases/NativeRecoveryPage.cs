using System;
using System.Collections.Generic;
namespace GamePlatform.Features.Purchases
{
    public sealed class NativeRecoveryPage
    {
        public NativeRecoveryPage(IReadOnlyList<NativeIntentState> intents, Guid? nextIntentId)
        { Intents = intents; NextIntentId = nextIntentId; }
        public IReadOnlyList<NativeIntentState> Intents { get; }
        public Guid? NextIntentId { get; }
    }
}
