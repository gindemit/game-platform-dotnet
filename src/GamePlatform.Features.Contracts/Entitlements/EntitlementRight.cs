using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Entitlements
{

    /// <summary>One opaque server-confirmed entitlement. Payload is not parsed or made authoritative by this feature.</summary>
    public sealed class EntitlementRight
    {
        private const int MaximumPayloadBytes = 16 * 1024;
        private readonly byte[] payload;

        public EntitlementRight(PlatformId entitlementId, long revision, EntitlementRightState state,
            EntitlementOriginReceipt originReceipt, long? expiresAtServerMilliseconds, ReadOnlySpan<byte> payload)
        {
            if (!entitlementId.IsValid)
                throw new ArgumentException("An entitlement identity is required.", nameof(entitlementId));
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            if (!Enum.IsDefined(typeof(EntitlementRightState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            OriginReceipt = originReceipt ?? throw new ArgumentNullException(nameof(originReceipt));
            if (expiresAtServerMilliseconds.HasValue && (expiresAtServerMilliseconds.Value < 0 || expiresAtServerMilliseconds.Value > 253_402_300_799_999L))
                throw new ArgumentOutOfRangeException(nameof(expiresAtServerMilliseconds));
            if ((state == EntitlementRightState.Expired || state == EntitlementRightState.Revoked) && payload.Length != 0)
                throw new ArgumentException("Revoked and expired rights carry no opaque payload.", nameof(payload));
            if (payload.Length > MaximumPayloadBytes)
                throw new ArgumentOutOfRangeException(nameof(payload));
            EntitlementId = entitlementId;
            Revision = revision;
            State = state;
            ExpiresAtServerMilliseconds = expiresAtServerMilliseconds;
            this.payload = payload.ToArray();
        }

        public PlatformId EntitlementId { get; }
        public long Revision { get; }
        public EntitlementRightState State { get; }
        public EntitlementOriginReceipt OriginReceipt { get; }
        public long? ExpiresAtServerMilliseconds { get; }
        public byte[] CopyPayload() => (byte[])payload.Clone();
    }
}
