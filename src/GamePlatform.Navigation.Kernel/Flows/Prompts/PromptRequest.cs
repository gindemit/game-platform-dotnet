using System;

namespace GamePlatform.Navigation.Kernel.Flows
{
    /// <summary>A bounded, deduplicated prompt request. Expiry and priority are supplied by its owner.</summary>
    public sealed class PromptRequest
    {
        public PromptRequest(string key, int priority, DateTimeOffset expiresAt)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A prompt key is required.", nameof(key));
            Key = key;
            Priority = priority;
            ExpiresAt = expiresAt;
        }
        public string Key { get; }
        public int Priority { get; }
        public DateTimeOffset ExpiresAt { get; }
    }
}
