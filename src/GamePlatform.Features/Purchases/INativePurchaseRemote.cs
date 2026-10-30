using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace GamePlatform.Features.Purchases
{
    public interface INativePurchaseRemote
    {
        Task<IReadOnlyList<NativeProduct>> ProductsAsync(CancellationToken token);
        Task<NativeIntentState> CreateIntentAsync(Guid intentKey, string productId, int productVersion, CancellationToken token);
        Task<NativeIntentState> SubmitProofAsync(Guid intentId, NativeProof proof, CancellationToken token);
        Task<NativeIntentState> RecoverProofAsync(Guid intentKey, NativeProof proof, CancellationToken token);
        Task<NativeIntentState> StatusAsync(Guid intentId, CancellationToken token);
        Task<NativeRecoveryPage> RecoveryAsync(Guid? afterIntentId, int limit, CancellationToken token);
    }
}
