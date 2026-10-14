namespace GamePlatform.Features.Contracts.Wallet
{

    public sealed class WalletSnapshot
    {
        public WalletSnapshot(WalletConfirmedBalance? confirmed, PendingWalletSpend? pending)
        {
            Confirmed = confirmed;
            Pending = pending;
        }
        public WalletConfirmedBalance? Confirmed { get; }
        public PendingWalletSpend? Pending { get; }
    }
}
