using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Transport.Http
{
    public sealed class ProvisioningHttpProvider : IProvisioningRemote
    {
        public const string MediaType = "application/vnd.gindemit.platform.v1+msgpack";
        private readonly BackendHttpConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly IWireCodec codec;
        private readonly IAuthSession auth;
        private readonly SemaphoreSlim refresh = new SemaphoreSlim(1, 1);

        public ProvisioningHttpProvider(BackendHttpConfiguration configuration, IHttpExecutor executor, IWireCodec codec, IAuthSession auth)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            if (string.IsNullOrWhiteSpace(auth.SessionKey) || auth.SessionKey.Length > 256) throw new ArgumentException("The auth session key is invalid.", nameof(auth));
        }

        public async Task<RemoteResult<ProvisioningSnapshot>> ProvisionAsync(AppId appId, Guid installationId, ClientStreamId streamId, CancellationToken cancellationToken)
        {
            if (!appId.IsValid) throw new ArgumentException("A valid app ID is required.", nameof(appId));
            if (installationId == Guid.Empty) throw new ArgumentException("A valid installation ID is required.", nameof(installationId));
            if (!streamId.IsValid) throw new ArgumentException("A valid stream ID is required.", nameof(streamId));
            var request = new ProvisionRequest(installationId, streamId.Value);
            byte[] body;
            try { body = codec.Encode(request); }
            catch { return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Protocol)); }
            if (body.Length == 0 || body.Length > 262_144) return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Protocol));

            AccessTokenSnapshot token;
            try { token = await auth.GetAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Cancelled)); }

            var response = await SendOnce(appId, body, token, cancellationToken).ConfigureAwait(false);
            if (response.Failure.HasValue) return RemoteResult<ProvisioningSnapshot>.Failed(response.Failure.Value);
            if (response.Response!.StatusCode == 401)
            {
                try
                {
                    await refresh.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var current = await auth.GetAsync(cancellationToken).ConfigureAwait(false);
                        token = current.Generation == token.Generation ? await auth.RefreshAsync(token.Generation, cancellationToken).ConfigureAwait(false) : current;
                    }
                    finally { refresh.Release(); }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Cancelled)); }
                catch { return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Authentication, 401)); }
                response = await SendOnce(appId, body, token, cancellationToken).ConfigureAwait(false);
                if (response.Failure.HasValue) return RemoteResult<ProvisioningSnapshot>.Failed(response.Failure.Value);
            }
            return Decode(appId, request, response.Response!);
        }

        private async Task<Attempt> SendOnce(AppId appId, byte[] body, AccessTokenSnapshot token, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer " + token.Value,
                ["Accept"] = MediaType,
                ["Content-Type"] = MediaType
            };
            var request = new HttpRequestData("POST", configuration.Resolve("v1/apps/" + appId + "/provision").AbsoluteUri, headers, body);
            try { return new Attempt(await executor.SendAsync(request, cancellationToken).ConfigureAwait(false)); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new Attempt(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)); }
            catch (HttpExecutionException error)
            {
                var kind = error.Certainty == HttpDeliveryCertainty.NotSent ? RemoteFailureKind.NotSent : RemoteFailureKind.OutcomeUncertain;
                return new Attempt(new RemoteFailure(kind));
            }
            catch { return new Attempt(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)); }
        }

        private RemoteResult<ProvisioningSnapshot> Decode(AppId appId, ProvisionRequest request, HttpResponseData response)
        {
            if (response.StatusCode != 200) return RemoteResult<ProvisioningSnapshot>.Failed(MapStatus(response));
            if (response.BodyLength == 0 || response.BodyLength > configuration.MaximumResponseBytes) return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode));
            if (!response.TryGetHeader("Content-Type", out var contentType) || !string.Equals(contentType, MediaType, StringComparison.OrdinalIgnoreCase)) return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode));
            if (response.TryGetHeader("Content-Encoding", out var encoding) && !string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase)) return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode));
            if (!response.TryGetHeader("X-Correlation-Id", out var correlation) || string.IsNullOrWhiteSpace(correlation) || correlation.Length > 128) return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode));
            ProvisionResponse value;
            try { value = codec.Decode<ProvisionResponse>(response.CopyBody()); }
            catch { return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode)); }
            if (value == null || value.ProtocolVersion != 1 || value.Membership.AppId != appId.Value || value.ClientStreamId != request.ClientStreamId || value.NextSequence <= 0) return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode));
            try
            {
                return RemoteResult<ProvisioningSnapshot>.Success(new ProvisioningSnapshot(
                    new PlatformUserId(value.Account.PlatformUserId), appId, value.Membership.Status,
                    new ClientStreamId(value.ClientStreamId), value.NextSequence, value.ServerTime));
            }
            catch { return RemoteResult<ProvisioningSnapshot>.Failed(new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode)); }
        }

        private static RemoteFailure MapStatus(HttpResponseData response)
        {
            var kind = response.StatusCode == 401 ? RemoteFailureKind.Authentication : response.StatusCode == 403 ? RemoteFailureKind.Authorization :
                response.StatusCode == 400 || response.StatusCode == 413 || response.StatusCode == 415 || response.StatusCode == 406 ? RemoteFailureKind.Validation :
                response.StatusCode == 409 ? RemoteFailureKind.Conflict : response.StatusCode == 429 ? RemoteFailureKind.RateLimited :
                response.StatusCode == 502 || response.StatusCode == 503 || response.StatusCode == 504 ? RemoteFailureKind.Dependency :
                response.StatusCode >= 500 ? RemoteFailureKind.Server : RemoteFailureKind.Protocol;
            TimeSpan? retryAfter = null;
            if (response.TryGetHeader("Retry-After", out var value) && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 && seconds <= 86_400) retryAfter = TimeSpan.FromSeconds(seconds);
            return new RemoteFailure(kind, response.StatusCode, retryAfter);
        }

        private readonly struct Attempt
        {
            public Attempt(HttpResponseData response) { Response = response; Failure = null; }
            public Attempt(RemoteFailure failure) { Response = null; Failure = failure; }
            public HttpResponseData? Response { get; }
            public RemoteFailure? Failure { get; }
        }
    }
}
