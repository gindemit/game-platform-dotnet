using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Purchases;
using GamePlatform.Transport.Abstractions;
namespace GamePlatform.Transport.Http
{
    /// <summary>Account-bound native purchase transport. It never falls back to a synthetic provider.</summary>
    public sealed class NativePurchaseHttpProvider : INativePurchaseRemote
    {
        private readonly AppId appId;
        private readonly PlatformUserId accountId;
        private readonly BackendHttpConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly IAuthSession auth;
        private readonly AuthRefreshCoordinator refresh;
        private readonly NativePurchaseMessagePackCodec codec = new NativePurchaseMessagePackCodec();
        public NativePurchaseHttpProvider(AppId appId, PlatformUserId accountId, BackendHttpConfiguration configuration,
            IHttpExecutor executor, IAuthSession auth, AuthRefreshCoordinator refresh)
        {
            if (!appId.IsValid || !accountId.IsValid)
                throw new ArgumentException("A valid app and account are required.");
            this.appId = appId;
            this.accountId = accountId;
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
        }
        public async Task<IReadOnlyList<NativeProduct>> ProductsAsync(CancellationToken token) =>
            codec.Products(await PostAsync("products", codec.ProductsRequest(), token).ConfigureAwait(false));
        public async Task<NativeIntentState> CreateIntentAsync(Guid intentKey, string productId, int productVersion, CancellationToken token) =>
            codec.Intent(await PostAsync("intents", codec.IntentRequest(intentKey, productId, productVersion), token).ConfigureAwait(false));
        public async Task<NativeIntentState> SubmitProofAsync(Guid intentId, NativeProof proof, CancellationToken token) =>
            codec.Intent(await PostAsync("proofs", codec.ProofRequest(intentId, proof), token).ConfigureAwait(false));
        public async Task<NativeIntentState> RecoverProofAsync(Guid intentKey, NativeProof proof, CancellationToken token) =>
            codec.Intent(await PostAsync("recover-proof", codec.RecoverProofRequest(intentKey, proof), token).ConfigureAwait(false));
        public async Task<NativeIntentState> StatusAsync(Guid intentId, CancellationToken token) =>
            codec.Intent(await PostAsync("status", codec.StatusRequest(intentId), token).ConfigureAwait(false));
        public async Task<NativeRecoveryPage> RecoveryAsync(Guid? afterIntentId, int limit, CancellationToken token) =>
            codec.Recovery(await PostAsync("recovery", codec.RecoveryRequest(afterIntentId, limit), token).ConfigureAwait(false));
        private async Task<byte[]> PostAsync(string route, byte[] body, CancellationToken token)
        {
            if (body.Length > 65536)
                throw new FormatException("Native request exceeds its bound.");
            AccessTokenSnapshot access;
            try
            { access = await auth.GetAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { throw new NativePurchaseRemoteException(401); }
            var response = await SendAsync(route, body, access, token).ConfigureAwait(false);
            if (response.StatusCode == 401)
            {
                try
                { access = await refresh.RefreshAsync(auth, accountId, access.Generation, token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch { throw new NativePurchaseRemoteException(401); }
                response = await SendAsync(route, body, access, token).ConfigureAwait(false);
            }
            if (response.StatusCode != 200 && response.StatusCode != 202)
                throw new NativePurchaseRemoteException(response.StatusCode);
            if (response.BodyLength <= 0 || response.BodyLength > 262144 || response.BodyLength > configuration.MaximumResponseBytes ||
                !response.TryGetHeader("Content-Type", out var media) || !string.Equals(media, PrivateSyncHttpProvider.MessagePackMediaType, StringComparison.OrdinalIgnoreCase) ||
                (response.TryGetHeader("Content-Encoding", out var encoding) && !string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase)) ||
                !response.TryGetHeader("X-Correlation-Id", out var correlation) || correlation.Length == 0 || correlation.Length > 128)
                throw new FormatException("Invalid native purchase response.");
            return response.CopyBody();
        }
        private Task<HttpResponseData> SendAsync(string route, byte[] body, AccessTokenSnapshot token, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer " + token.Value,
                ["Accept"] = PrivateSyncHttpProvider.MessagePackMediaType,
                ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType
            };
            return SendBoundedAsync(new HttpRequestData("POST", configuration.Resolve("v1/apps/" + appId + "/store/native/" + route).AbsoluteUri, headers, body), cancellationToken);
        }
        private async Task<HttpResponseData> SendBoundedAsync(HttpRequestData request, CancellationToken token)
        {
            try
            { return await executor.SendAsync(request, token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { throw new NativePurchaseRemoteException(0); }
        }
    }
}
