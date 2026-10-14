#nullable enable

namespace GamePlatform.Serialization.MessagePack
{
    /// <summary>
    /// The single store.offer.purchase.v1 and quest.claim.v1 switch shared by fingerprint, push and receipt composition.
    /// </summary>
    public sealed class StoreQuestCapability
    {
        public StoreQuestCapability(bool enabled)
        {
            Enabled = enabled;
            Codec = enabled ? new StoreQuestMessagePackCodec() : null;
        }

        public bool Enabled { get; }

        public StoreQuestMessagePackCodec? Codec { get; }

        public CanonicalCommandFingerprint CreateFingerprint() => new CanonicalCommandFingerprint(Enabled);
    }
}
