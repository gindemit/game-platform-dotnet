using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{

    /// <summary>Feature-owned remote boundary. Implementations must authenticate and validate funds online before authorizing admission.</summary>
    public interface IWalletRemote
    {
        Task<WalletRemoteRead> ReadAsync(WalletBalanceRequest request, CancellationToken cancellationToken);
        Task<WalletSpendAuthorization> AuthorizeSpendAsync(WalletSpendRequest request, CancellationToken cancellationToken);
    }
}
