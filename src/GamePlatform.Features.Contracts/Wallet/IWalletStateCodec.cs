using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    public interface IWalletStateCodec
    {
        byte[] EncodeConfirmed(WalletConfirmedBalance confirmed);
        WalletConfirmedBalance DecodeConfirmed(long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions);
        byte[] EncodePending(PendingWalletSpend pending);
        PendingWalletSpend DecodePending(long localRevision, ReadOnlySpan<byte> payload);
        byte[] EncodeSpendCommand(WalletSpendIntent intent);
        void ValidateExtensions(ReadOnlySpan<byte> extensions);
    }
}
