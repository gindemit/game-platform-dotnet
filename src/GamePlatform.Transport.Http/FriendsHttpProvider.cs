#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Friends;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Wire.Contracts;
using W = GamePlatform.Wire.Contracts.Friends;

namespace GamePlatform.Transport.Http
{
    /// <summary>Authenticated Friends provider and closed durable codec. Requests never fall back to JSON.</summary>
    public sealed class FriendsHttpProvider : IFriendsRemote, IFriendsStateCodec
    {
        private readonly AppId appId;
        private readonly PlatformUserId accountId;
        private readonly BackendHttpConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly IAuthSession auth;
        private readonly AuthRefreshCoordinator refresh;
        private readonly FriendsMessagePackCodec codec = new FriendsMessagePackCodec();
        private readonly MessagePackWireCodec errors = new MessagePackWireCodec();

        public FriendsHttpProvider(AppId appId, PlatformUserId accountId, BackendHttpConfiguration configuration,
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

        public Task<RemoteResult<FriendsResponse>> QueryAsync(FriendsQuery query, CancellationToken cancellationToken)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            return SendAsync("query", codec.EncodeQuery(new W.FriendsQuery(query.View, query.PublicCode, query.Cursor, query.PageSize)), null, query.View, cancellationToken);
        }
        public Task<RemoteResult<FriendsResponse>> ExecuteAsync(FriendsCommand command, CancellationToken cancellationToken)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            return SendAsync("commands", EncodeCommand(command), command.OperationId.Value, null, cancellationToken);
        }
        private readonly FriendsStateCodec stateCodec = new FriendsStateCodec();
        public byte[] EncodeResponse(FriendsResponse response) => stateCodec.EncodeResponse(response);
        public FriendsResponse DecodeResponse(byte[] bytes) => stateCodec.DecodeResponse(bytes);
        public byte[] EncodeCommand(FriendsCommand command) => stateCodec.EncodeCommand(command);
        public FriendsCommand DecodeCommand(byte[] bytes) => stateCodec.DecodeCommand(bytes);

        private async Task<RemoteResult<FriendsResponse>> SendAsync(string route, byte[] body, Guid? operationId, string? view, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AccessTokenSnapshot token;
            try
            {
                token = await auth.GetAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Failure(RemoteFailureKind.Cancelled); }
            catch { return Failure(RemoteFailureKind.Authentication); }
            var attempt = await SendOnceAsync(route, body, token, operationId.HasValue, cancellationToken).ConfigureAwait(false);
            if (attempt.Failure.HasValue)
                return RemoteResult<FriendsResponse>.Failed(attempt.Failure.Value);
            if (attempt.Response!.StatusCode == 401)
            {
                try
                {
                    token = await refresh.RefreshAsync(auth, accountId, token.Generation, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Failure(RemoteFailureKind.Cancelled); }
                catch { return Failure(RemoteFailureKind.Authentication, 401); }
                attempt = await SendOnceAsync(route, body, token, operationId.HasValue, cancellationToken).ConfigureAwait(false);
                if (attempt.Failure.HasValue)
                    return RemoteResult<FriendsResponse>.Failed(attempt.Failure.Value);
            }
            var response = attempt.Response!;
            if (response.StatusCode != 200)
                throw DecodeFailure(response);
            if (!ValidResponse(response))
                return Failure(RemoteFailureKind.Protocol, response.StatusCode);
            try
            {
                var value = DecodeResponse(response.CopyBody());
                if (value.AppId != appId.Value || value.AccountId != accountId.Value || value.OperationId != operationId ||
                    value.View != view)
                    return Failure(RemoteFailureKind.Protocol, response.StatusCode);
                return RemoteResult<FriendsResponse>.Success(value);
            }
            catch { return Failure(RemoteFailureKind.Protocol, response.StatusCode); }
        }

        private async Task<Attempt> SendOnceAsync(string route, byte[] body, AccessTokenSnapshot token, bool mutation, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer " + token.Value,
                ["Accept"] = PrivateSyncHttpProvider.MessagePackMediaType,
                ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType
            };
            try
            {
                var response = await executor.SendAsync(new HttpRequestData("POST", configuration.Resolve("v1/apps/" + appId + "/friends/" + route).AbsoluteUri, headers, body), cancellationToken).ConfigureAwait(false);
                return new Attempt(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new Attempt(new RemoteFailure(mutation ? RemoteFailureKind.OutcomeUncertain : RemoteFailureKind.Cancelled));
            }
            catch (HttpExecutionException error)
            {
                return new Attempt(new RemoteFailure(error.Certainty == HttpDeliveryCertainty.NotSent ? RemoteFailureKind.NotSent : mutation ? RemoteFailureKind.OutcomeUncertain : RemoteFailureKind.Dependency));
            }
            catch { return new Attempt(new RemoteFailure(mutation ? RemoteFailureKind.OutcomeUncertain : RemoteFailureKind.Dependency)); }
        }

        private bool ValidResponse(HttpResponseData response) => response.BodyLength > 0 && response.BodyLength <= configuration.MaximumResponseBytes &&
            response.TryGetHeader("Content-Type", out var media) && string.Equals(media, PrivateSyncHttpProvider.MessagePackMediaType, StringComparison.OrdinalIgnoreCase) &&
            (!response.TryGetHeader("Content-Encoding", out var encoding) || string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase)) &&
            response.TryGetHeader("X-Correlation-Id", out var correlation) && correlation.Length > 0 && correlation.Length <= 128;

        private FriendsOperationException DecodeFailure(HttpResponseData response)
        {
            if (!ValidResponse(response))
                return new FriendsOperationException(new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode), "friends.protocol");
            var kind = response.StatusCode == 401 ? RemoteFailureKind.Authentication : response.StatusCode == 403 ? RemoteFailureKind.Authorization :
                response.StatusCode == 409 ? RemoteFailureKind.Conflict : response.StatusCode == 429 ? RemoteFailureKind.RateLimited :
                response.StatusCode == 404 ? RemoteFailureKind.Unavailable : response.StatusCode >= 500 ? RemoteFailureKind.Dependency : RemoteFailureKind.Validation;
            TimeSpan? retry = null;
            if (response.TryGetHeader("Retry-After", out var retryText) && int.TryParse(retryText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 && seconds <= 86400)
                retry = TimeSpan.FromSeconds(seconds);
            string code = "friends.unavailable";
            Guid? retainedOperation = null;
            long? delay = null;
            if (ValidResponse(response))
            {
                try
                {
                    var error = errors.Decode<ErrorResponse>(response.CopyBody());
                    if (error.CorrelationId != response.Headers["X-Correlation-Id"])
                        return new FriendsOperationException(new RemoteFailure(RemoteFailureKind.Protocol), "friends.protocol");
                    code = error.Error.Code;
                    var detail = error.Error.Details;
                    if (!error.Error.Retryable && code.StartsWith("friends.", StringComparison.Ordinal) && code != "friends.identity_conflict" && code != "friends.operation_identity_conflict" &&
                        (error.Error.Category == "validation" || error.Error.Category == "conflict" || error.Error.Category == "rate_limit") &&
                        detail.IsPresent && detail.Value.TryGetValue("receiptRetained", out var retained) && retained.Kind == ErrorDetailKind.Boolean && retained.Boolean &&
                        detail.Value.TryGetValue("operationId", out var operation) && operation.Kind == ErrorDetailKind.String &&
                        Guid.TryParseExact(operation.Text, "D", out var id) && id != Guid.Empty && id.ToString("D")[14] == '7')
                        retainedOperation = id;
                    if (detail.IsPresent && detail.Value.TryGetValue("retryAfterMilliseconds", out var retryDetail) && retryDetail.Kind == ErrorDetailKind.Integer && retryDetail.Integer >= 0)
                        delay = retryDetail.Integer;
                    else if (error.Error.RetryAfterMilliseconds.IsPresent)
                        delay = error.Error.RetryAfterMilliseconds.Value;
                    if (delay.HasValue)
                        retry = TimeSpan.FromMilliseconds(delay.Value);
                }
                catch { return new FriendsOperationException(new RemoteFailure(RemoteFailureKind.Protocol), "friends.protocol"); }
            }
            return new FriendsOperationException(new RemoteFailure(kind, response.StatusCode, retry), code, retainedOperation, delay);
        }
        private static RemoteResult<FriendsResponse> Failure(RemoteFailureKind kind, int? status = null) => RemoteResult<FriendsResponse>.Failed(new RemoteFailure(kind, status));
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
            public HttpResponseData? Response
            {
                get;
            }
            public RemoteFailure? Failure
            {
                get;
            }
        }
    }
}
