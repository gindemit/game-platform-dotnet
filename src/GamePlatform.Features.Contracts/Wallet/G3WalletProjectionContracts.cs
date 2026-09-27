#nullable enable
using System;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{
    /// <summary>
    /// Frozen G3 confirmed-wallet projection. Its semantic currency identity is display data only;
    /// it deliberately has no catalog definition, spend, credit, grant, or authority surface.
    /// </summary>
    public sealed class G3ConfirmedWalletProjection
    {
        public G3ConfirmedWalletProjection(SemanticId currencyId, long balance, long revision)
        {
            if (!currencyId.IsValid) throw new ArgumentException("A semantic currency ID is required.", nameof(currencyId));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            CurrencyId = currencyId;
            Balance = balance;
            Revision = revision;
        }

        public SemanticId CurrencyId { get; }
        public long Balance { get; }
        public long Revision { get; }
    }

    /// <summary>Exact owner/view/generation fenced cached-wallet read key for the G3 projection.</summary>
    public sealed class G3WalletProjectionRequest
    {
        public G3WalletProjectionRequest(ScopedOwnerContext owner, SemanticId currencyId)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!currencyId.IsValid) throw new ArgumentException("A semantic currency ID is required.", nameof(currencyId));
            Owner = owner;
            CurrencyId = currencyId;
        }

        public ScopedOwnerContext Owner { get; }
        public SemanticId CurrencyId { get; }
    }

    /// <summary>Explicit private-feed removal semantics. Both kinds read as Missing and never synthesize a zero balance.</summary>
    public enum G3WalletProjectionRemoval
    {
        RemoveFromView = 0,
        Reset = 1
    }

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
