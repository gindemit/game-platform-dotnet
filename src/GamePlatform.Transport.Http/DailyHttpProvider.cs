using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Daily;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Wire.Contracts;

namespace GamePlatform.Transport.Http
{
    /// <summary>Authenticated daily status provider; a missing route is an unavailable capability, never a JSON fallback.</summary>
    public sealed class DailyHttpProvider : IDailyRemote
    {
        private readonly AppId appId;
        private readonly PlatformUserId accountId;
        private readonly BackendHttpConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly IAuthSession auth;
        private readonly AuthRefreshCoordinator refresh;
        private readonly DailyMessagePackCodec codec = new DailyMessagePackCodec();
        private readonly MessagePackWireCodec errors = new MessagePackWireCodec();

        public DailyHttpProvider(AppId appId, PlatformUserId accountId, BackendHttpConfiguration configuration,
            IHttpExecutor executor, IAuthSession auth, AuthRefreshCoordinator refresh)
        {
            if (!appId.IsValid || !accountId.IsValid)
                throw new ArgumentException("Valid authenticated app/account identities are required.");
            this.appId = appId;
            this.accountId = accountId;
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            if (string.IsNullOrWhiteSpace(auth.SessionKey) || auth.SessionKey.Length > 256)
                throw new ArgumentException("Invalid auth session key.", nameof(auth));
        }

        public async Task<RemoteResult<DailyStatus>> StatusAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AccessTokenSnapshot token;
            try
            {
                token = await auth.GetAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Failure(RemoteFailureKind.Cancelled); }
            catch { return Failure(RemoteFailureKind.Authentication); }
            var body = codec.EncodeStatusRequest();
            var attempt = await SendOnceAsync(body, token, cancellationToken).ConfigureAwait(false);
            if (attempt.Failure.HasValue)
                return RemoteResult<DailyStatus>.Failed(attempt.Failure.Value);
            if (attempt.Response!.StatusCode == 401)
            {
                try
                {
                    token = await refresh.RefreshAsync(auth, accountId, token.Generation, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Failure(RemoteFailureKind.Cancelled); }
                catch { return Failure(RemoteFailureKind.Authentication, 401); }
                attempt = await SendOnceAsync(body, token, cancellationToken).ConfigureAwait(false);
                if (attempt.Failure.HasValue)
                    return RemoteResult<DailyStatus>.Failed(attempt.Failure.Value);
            }
            var response = attempt.Response!;
            if (response.StatusCode != 200)
                throw DecodeFailure(response);
            if (!ValidResponse(response))
                return Failure(RemoteFailureKind.Protocol, response.StatusCode);
            try
            {
                return RemoteResult<DailyStatus>.Success(DailyModelMapper.ToDomain(codec.DecodeStatusResponse(response.CopyBody())));
            }
            catch { return Failure(RemoteFailureKind.Protocol, response.StatusCode); }
        }

        private async Task<Attempt> SendOnceAsync(byte[] body, AccessTokenSnapshot token, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer " + token.Value,
                ["Accept"] = PrivateSyncHttpProvider.MessagePackMediaType,
                ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType
            };
            try
            {
                var response = await executor.SendAsync(new HttpRequestData("POST", configuration.Resolve("v1/apps/" + appId + "/daily/status").AbsoluteUri, headers, body), cancellationToken).ConfigureAwait(false);
                return new Attempt(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled));
            }
            catch (HttpExecutionException error)
            {
                return new Attempt(new RemoteFailure(error.Certainty == HttpDeliveryCertainty.NotSent ? RemoteFailureKind.NotSent : RemoteFailureKind.Dependency));
            }
            catch { return new Attempt(new RemoteFailure(RemoteFailureKind.Dependency)); }
        }

        private bool ValidResponse(HttpResponseData response) => response.BodyLength > 0 && response.BodyLength <= configuration.MaximumResponseBytes &&
            response.TryGetHeader("Content-Type", out var media) && string.Equals(media, PrivateSyncHttpProvider.MessagePackMediaType, StringComparison.OrdinalIgnoreCase) &&
            (!response.TryGetHeader("Content-Encoding", out var encoding) || string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase)) &&
            response.TryGetHeader("X-Correlation-Id", out var correlation) && correlation.Length > 0 && correlation.Length <= 128;

        private DailyOperationException DecodeFailure(HttpResponseData response)
        {
            if (!ValidResponse(response))
                return response.StatusCode == 404
                    ? new DailyOperationException(new RemoteFailure(RemoteFailureKind.Unavailable, 404), "daily_unavailable")
                    : new DailyOperationException(new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode), "daily_protocol");
            var kind = response.StatusCode == 401 ? RemoteFailureKind.Authentication : response.StatusCode == 403 ? RemoteFailureKind.Authorization :
                response.StatusCode == 409 ? RemoteFailureKind.Conflict : response.StatusCode == 429 ? RemoteFailureKind.RateLimited :
                response.StatusCode == 404 ? RemoteFailureKind.Unavailable : response.StatusCode >= 500 ? RemoteFailureKind.Dependency : RemoteFailureKind.Validation;
            TimeSpan? retry = null;
            if (response.TryGetHeader("Retry-After", out var retryText) && int.TryParse(retryText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 && seconds <= 86400)
                retry = TimeSpan.FromSeconds(seconds);
            string code;
            try
            {
                var error = errors.Decode<ErrorResponse>(response.CopyBody());
                if (error.CorrelationId != response.Headers["X-Correlation-Id"])
                    return new DailyOperationException(new RemoteFailure(RemoteFailureKind.Protocol), "daily_protocol");
                code = error.Error.Code;
            }
            catch { return new DailyOperationException(new RemoteFailure(RemoteFailureKind.Protocol), "daily_protocol"); }
            return new DailyOperationException(new RemoteFailure(kind, response.StatusCode, retry), code);
        }
        private static RemoteResult<DailyStatus> Failure(RemoteFailureKind kind, int? status = null) => RemoteResult<DailyStatus>.Failed(new RemoteFailure(kind, status));
        private readonly struct Attempt
        {
            public Attempt(HttpResponseData response)
            {
                Response = response;
                Failure = null;
            }
            public Attempt(RemoteFailure failure)
            {
                Response = null;
                Failure = failure;
            }
            public HttpResponseData? Response { get; }
            public RemoteFailure? Failure { get; }
        }
    }
}
