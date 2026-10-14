using System;

namespace GamePlatform.Features.Contracts.Entitlements
{

    /// <summary>Feature-owned codec seam; production wire/storage adapters remain separate owners.</summary>
    public interface IEntitlementStateCodec
    {
        byte[] EncodeConfirmed(EntitlementConfirmedSnapshot snapshot);
        EntitlementConfirmedSnapshot DecodeConfirmed(long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions);
        void ValidateSupportedExtensions(ReadOnlySpan<byte> extensions);
    }
}
