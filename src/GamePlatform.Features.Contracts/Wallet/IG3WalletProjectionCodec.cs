using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    /// <summary>
    /// Versioned bounded local projection codec. Implementations encode only the frozen confirmed record;
    /// the service supplies the envelope schema version and rejects extension bytes.
    /// </summary>
    public interface IG3WalletProjectionCodec
    {
        int SchemaVersion { get; }
        byte[] Encode(G3ConfirmedWalletProjection projection);
        G3ConfirmedWalletProjection Decode(int schemaVersion, ReadOnlySpan<byte> payload);
    }
}
