#nullable enable
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
    /// <summary>
    /// MessagePack-only semantic transport for exactly one immutable command.
    /// The caller retains command identity and resolves uncertain delivery through
    /// the existing durable sender and receipt path.
    /// </summary>
    public sealed class CommandPushHttpProvider : ICommandRemote
    {
        private readonly AppId appId;
        private readonly PlatformUserId accountId;
        private readonly BackendHttpConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly IWireCodec codec;
        private readonly IAuthSession auth;
        private readonly AuthRefreshCoordinator refresh;

        public CommandPushHttpProvider(
            AppId appId,
            PlatformUserId accountId,
            BackendHttpConfiguration configuration,
            IHttpExecutor executor,
            IWireCodec codec,
            IAuthSession auth,
            AuthRefreshCoordinator refresh)
        {
            if (!appId.IsValid) throw new ArgumentException("A valid app ID is required.", nameof(appId));
            if (!accountId.IsValid) throw new ArgumentException("A valid account ID is required.", nameof(accountId));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            if (string.IsNullOrWhiteSpace(auth.SessionKey) || auth.SessionKey.Length > 256)
                throw new ArgumentException("The auth session key is invalid.", nameof(auth));
            this.appId = appId;
            this.accountId = accountId;
        }

        public async Task<RemoteResult<RemoteCommandOutcome>> SendAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!TryCreateRequest(command, out var request)) return ProtocolFailure();

            byte[] body;
            try { body = codec.Encode(request); }
            catch { return ProtocolFailure(); }
            if (body.Length == 0 || body.Length > 262_144) return ProtocolFailure();

            var attempt = await SendAuthenticatedAsync(body, cancellationToken).ConfigureAwait(false);
            if (attempt.Failure.HasValue) return RemoteResult<RemoteCommandOutcome>.Failed(attempt.Failure.Value);
            var response = attempt.Response!;
            if (response.StatusCode != 200) return RemoteResult<RemoteCommandOutcome>.Failed(DecodeFailure(response));
            if (!ValidProtocolResponse(response)) return ProtocolFailure(response.StatusCode);

            PushResponse value;
            try { value = codec.Decode<PushResponse>(response.CopyBody()); }
            catch { return ProtocolFailure(response.StatusCode); }
            if (value == null || value.ProtocolVersion != 1 || value.ClientStreamId != command.StreamId.Value ||
                value.OperationResults == null || value.OperationResults.Count != 1 || value.FinalizedThrough < 0 ||
                value.ServerTime < 0 || value.ServerTime > 253_402_300_799_999L)
                return ProtocolFailure(response.StatusCode);

            try { return MapResult(command, value, response); }
            catch { return ProtocolFailure(response.StatusCode); }
        }

        private bool TryCreateRequest(RemoteCommand command, out PushRequest request)
        {
            request = null!;
            if (command.SchemaVersion != 1 || command.FingerprintVersion != 1 || command.Fingerprint.Length != 32)
                return false;
            var semanticBody = command.SemanticBody;
            if (semanticBody.Length == 0 || semanticBody.Length > 262_144) return false;

            IPushPayload payload;
            try
            {
                switch (command.OperationKind)
                {
                    case "profile.patch":
                        payload = codec.Decode<ProfilePatchCommand>(semanticBody);
                        break;
                    case "gameplay.session.completed":
                        payload = codec.Decode<GameplayCompletionCommand>(semanticBody);
                        break;
                    default:
                        return false;
                }
                if (payload == null) return false;
                request = new PushRequest(command.StreamId.Value, new[]
                {
                    new PushOperation(command.OperationId.Value, command.InstallationId, command.Sequence,
                        command.OperationKind, command.ClientCreatedAt, payload)
                });
                return true;
            }
            catch { return false; }
        }

        private RemoteResult<RemoteCommandOutcome> MapResult(RemoteCommand command, PushResponse response, HttpResponseData httpResponse)
        {
            var result = response.OperationResults[0];
            if (result == null) return ProtocolFailure(httpResponse.StatusCode);
            switch (result)
            {
                case PushAccepted accepted when Matches(command, accepted.OperationId, accepted.Sequence) &&
                    response.FinalizedThrough >= command.Sequence && accepted.FeedRevision >= 0 &&
                    IsExpectedAcceptedResult(command.OperationKind, accepted.Result):
                    return Terminal(RemoteCommandStatus.Accepted, accepted, httpResponse.StatusCode);

                case PushTerminalRejected rejected when Matches(command, rejected.OperationId, rejected.Sequence) &&
                    response.FinalizedThrough >= command.Sequence && rejected.Error != null && !rejected.Error.Retryable:
                    return Terminal(RemoteCommandStatus.TerminalRejected, rejected, httpResponse.StatusCode);

                case PushRetryable retryable when Matches(command, retryable.OperationId, retryable.Sequence) &&
                    response.FinalizedThrough < command.Sequence && retryable.Error != null && retryable.Error.Retryable:
                    return Retryable(retryable.Error, httpResponse);

                case PushWaiting waiting when Matches(command, waiting.OperationId, waiting.Sequence) &&
                    response.FinalizedThrough < command.Sequence && waiting.ExpectedSequence > 0:
                    return RemoteResult<RemoteCommandOutcome>.Failed(new RemoteFailure(RemoteFailureKind.Conflict, httpResponse.StatusCode));

                case PushUpgradeRequired upgrade when Matches(command, upgrade.OperationId, upgrade.Sequence) &&
                    response.FinalizedThrough < command.Sequence && upgrade.MinimumProtocolVersion >= 2 &&
                    upgrade.Error != null && !upgrade.Error.Retryable:
                    return RemoteResult<RemoteCommandOutcome>.Failed(new RemoteFailure(RemoteFailureKind.Unavailable, httpResponse.StatusCode));

                default:
                    return ProtocolFailure(httpResponse.StatusCode);
            }
        }

        private RemoteResult<RemoteCommandOutcome> Terminal(RemoteCommandStatus status, IPushResult result, int statusCode)
        {
            byte[] terminal;
            try { terminal = codec.Encode(result); }
            catch { return ProtocolFailure(statusCode); }
            if (terminal.Length == 0 || terminal.Length > 65_536) return ProtocolFailure(statusCode);
            return RemoteResult<RemoteCommandOutcome>.Success(new RemoteCommandOutcome(status, terminal));
        }

        private RemoteResult<RemoteCommandOutcome> Retryable(CommonError error, HttpResponseData response)
        {
            if (!TryRetryAfter(error, response, out var retryAfter)) return ProtocolFailure(response.StatusCode);
            return RemoteResult<RemoteCommandOutcome>.Failed(new RemoteFailure(MapCategory(error.Category), response.StatusCode, retryAfter));
        }

        private async Task<Attempt> SendAuthenticatedAsync(byte[] body, CancellationToken cancellationToken)
        {
            AccessTokenSnapshot token;
            try { token = await auth.GetAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled)); }
            catch { return new Attempt(new RemoteFailure(RemoteFailureKind.Authentication)); }

            var response = await SendOnce(body, token, cancellationToken).ConfigureAwait(false);
            if (response.Failure.HasValue || response.Response!.StatusCode != 401) return response;
            try { token = await refresh.RefreshAsync(auth, accountId, token.Generation, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled)); }
            catch { return new Attempt(new RemoteFailure(RemoteFailureKind.Authentication, 401)); }
            return await SendOnce(body, token, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Attempt> SendOnce(byte[] body, AccessTokenSnapshot token, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer " + token.Value,
                ["Accept"] = PrivateSyncHttpProvider.MessagePackMediaType,
                ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType
            };
            var request = new HttpRequestData("POST", configuration.Resolve("v1/apps/" + appId + "/sync/push").AbsoluteUri, headers, body);
            try { return new Attempt(await executor.SendAsync(request, cancellationToken).ConfigureAwait(false)); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new Attempt(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)); }
            catch (HttpExecutionException error)
            {
                return new Attempt(new RemoteFailure(error.Certainty == HttpDeliveryCertainty.NotSent
                    ? RemoteFailureKind.NotSent : RemoteFailureKind.OutcomeUncertain));
            }
            catch { return new Attempt(new RemoteFailure(RemoteFailureKind.OutcomeUncertain)); }
        }

        private RemoteFailure DecodeFailure(HttpResponseData response)
        {
            if (response.BodyLength > configuration.MaximumResponseBytes) return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode);
            var mapped = MapStatus(response);
            if (!HasProtocolMediaType(response)) return mapped;
            if (!ValidProtocolResponse(response)) return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode);
            try
            {
                var error = codec.Decode<ErrorResponse>(response.CopyBody());
                if (error == null || error.ProtocolVersion != 1 || string.IsNullOrWhiteSpace(error.CorrelationId) ||
                    !response.TryGetHeader("X-Correlation-Id", out var correlation) ||
                    !string.Equals(error.CorrelationId, correlation, StringComparison.Ordinal) ||
                    !TryRetryAfter(error.Error, response, out var retryAfter))
                    return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode);
                return new RemoteFailure(MapCategory(error.Error.Category), response.StatusCode, retryAfter);
            }
            catch { return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode); }
        }

        private bool ValidProtocolResponse(HttpResponseData response) =>
            response.BodyLength > 0 && response.BodyLength <= configuration.MaximumResponseBytes && HasProtocolMediaType(response) &&
            (!response.TryGetHeader("Content-Encoding", out var encoding) || string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase)) &&
            response.TryGetHeader("X-Correlation-Id", out var correlation) && !string.IsNullOrWhiteSpace(correlation) && correlation.Length <= 128;

        private static bool Matches(RemoteCommand command, Guid operationId, long sequence) =>
            operationId == command.OperationId.Value && sequence == command.Sequence;

        private static bool IsExpectedAcceptedResult(string operationKind, IPushAcceptedResult result) =>
            (operationKind == "profile.patch" && result is PushProfileUpdatedResult profile && profile.ProfileRevision >= 0) ||
            (operationKind == "gameplay.session.completed" && result is PushGameplayCompletionRecordedResult);

        private static bool TryRetryAfter(CommonError error, HttpResponseData response, out TimeSpan? retryAfter)
        {
            retryAfter = null;
            var hasHeader = response.TryGetHeader("Retry-After", out var retryAfterHeader);
            long? headerMilliseconds = null;
            if (hasHeader)
            {
                if (!int.TryParse(retryAfterHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds < 0 || seconds > 86_400)
                    return false;
                headerMilliseconds = checked((long)seconds * 1000);
            }

            if (error.RetryAfterMilliseconds.IsPresent)
            {
                var milliseconds = error.RetryAfterMilliseconds.Value;
                if (milliseconds < 0 || milliseconds > 86_400_000) return false;
                if (headerMilliseconds.HasValue && headerMilliseconds.Value != checked(((long)milliseconds + 999) / 1000 * 1000))
                    return false;
                retryAfter = TimeSpan.FromMilliseconds(milliseconds);
            }
            else if (headerMilliseconds.HasValue)
            {
                retryAfter = TimeSpan.FromMilliseconds(headerMilliseconds.Value);
            }

            return error.Category != "rate_limit" || hasHeader;
        }

        private bool HasProtocolMediaType(HttpResponseData response) =>
            response.TryGetHeader("Content-Type", out var contentType) &&
            string.Equals(contentType, PrivateSyncHttpProvider.MessagePackMediaType, StringComparison.OrdinalIgnoreCase);

        private static RemoteFailure MapStatus(HttpResponseData response)
        {
            var kind = response.StatusCode == 401 ? RemoteFailureKind.Authentication : response.StatusCode == 403 ? RemoteFailureKind.Authorization :
                response.StatusCode == 400 || response.StatusCode == 405 || response.StatusCode == 406 || response.StatusCode == 413 || response.StatusCode == 415 ? RemoteFailureKind.Validation :
                response.StatusCode == 409 ? RemoteFailureKind.Conflict : response.StatusCode == 429 ? RemoteFailureKind.RateLimited :
                response.StatusCode == 502 || response.StatusCode == 503 || response.StatusCode == 504 ? RemoteFailureKind.Dependency :
                response.StatusCode >= 500 ? RemoteFailureKind.Server : RemoteFailureKind.Protocol;
            return new RemoteFailure(kind, response.StatusCode, RetryAfter(response));
        }

        private static RemoteFailureKind MapCategory(string category) => category == "authentication" ? RemoteFailureKind.Authentication :
            category == "authorization" ? RemoteFailureKind.Authorization : category == "validation" ? RemoteFailureKind.Validation :
            category == "conflict" ? RemoteFailureKind.Conflict : category == "rate_limit" ? RemoteFailureKind.RateLimited :
            category == "dependency" ? RemoteFailureKind.Dependency : category == "internal" ? RemoteFailureKind.Server : RemoteFailureKind.Protocol;

        private static TimeSpan? RetryAfter(HttpResponseData response)
        {
            if (response.TryGetHeader("Retry-After", out var value) && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 && seconds <= 86_400)
                return TimeSpan.FromSeconds(seconds);
            return null;
        }

        private static RemoteResult<RemoteCommandOutcome> ProtocolFailure(int? status = null) =>
            RemoteResult<RemoteCommandOutcome>.Failed(new RemoteFailure(RemoteFailureKind.Protocol, status));

        private readonly struct Attempt
        {
            public Attempt(HttpResponseData response) { Response = response; Failure = null; }
            public Attempt(RemoteFailure failure) { Response = null; Failure = failure; }
            public HttpResponseData? Response { get; }
            public RemoteFailure? Failure { get; }
        }
    }
}
