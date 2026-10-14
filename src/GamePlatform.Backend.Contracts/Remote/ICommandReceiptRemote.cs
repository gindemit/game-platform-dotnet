using System.Threading;
using System.Threading.Tasks;

namespace GamePlatform.Backend.Contracts.Remote
{

    public interface ICommandReceiptRemote
    {
        Task<RemoteResult<RemoteCommandReceipt>> LookupAsync(RemoteCommand command, CancellationToken cancellationToken);
    }
}
