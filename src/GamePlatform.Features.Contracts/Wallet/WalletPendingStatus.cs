using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    public enum WalletPendingStatus
    {
        AwaitingReceipt = 0, AcceptedAwaitingPull = 1, Rejected = 2
    }
}
