using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Features.Contracts.Wallet;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;

namespace GamePlatform.Features.Wallet
{

    public sealed class G3WalletProjectionConflictException : InvalidOperationException
    {
        public G3WalletProjectionConflictException(string message) : base(message) { }
    }
}
