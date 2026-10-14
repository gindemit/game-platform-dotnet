using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{
    /// <summary>Stable server-defined currency identity. It carries neither a balance nor authority.</summary>
    public sealed class WalletCurrency
    {
        public WalletCurrency(PlatformId definitionId, SemanticId semanticKey)
        {
            if (!definitionId.IsValid)
                throw new ArgumentException("A stable currency definition ID is required.", nameof(definitionId));
            if (!semanticKey.IsValid)
                throw new ArgumentException("A currency semantic key is required.", nameof(semanticKey));
            DefinitionId = definitionId;
            SemanticKey = semanticKey;
        }
        public PlatformId DefinitionId { get; }
        public SemanticId SemanticKey { get; }
    }
}
