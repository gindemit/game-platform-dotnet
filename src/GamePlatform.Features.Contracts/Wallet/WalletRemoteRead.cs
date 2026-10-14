using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    public sealed class WalletRemoteRead
    {
        public WalletRemoteRead(ScopedOwnerContext owner, WalletConfirmedBalance confirmed)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            Owner = owner;
            Confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed));
        }
        public ScopedOwnerContext Owner { get; }
        public WalletConfirmedBalance Confirmed { get; }
    }
}
