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
    /// Portable semantic lookup for a retained terminal command receipt. This
    /// provider is read-only and does not register a production transport.
    /// </summary>
    public sealed class CommandReceiptHttpProvider : ICommandReceiptRemote
    {
        private readonly AppId appId;
        private readonly PlatformUserId accountId;
        private readonly Guid installationId;
        private readonly BackendHttpConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly IWireCodec codec;
        private readonly IAuthSession auth;
        private readonly AuthRefreshCoordinator refresh;
        private readonly string mediaType;

        public CommandReceiptHttpProvider(
            AppId appId,
            PlatformUserId accountId,
            Guid installationId,
            BackendHttpConfiguration configuration,
            IHttpExecutor executor,
            IWireCodec codec,
            IAuthSession auth,
            AuthRefreshCoordinator refresh,
            BackendWireRepresentation representation = BackendWireRepresentation.MessagePack)
        {
            if (!appId.IsValid) throw new ArgumentException("A valid app ID is required.", nameof(appId));
            if (!accountId.IsValid) throw new ArgumentException("A valid account ID is required.", nameof(accountId));
            if (installationId == Guid.Empty) throw new ArgumentException("A valid installation ID is required.", nameof(installationId));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            if (string.IsNullOrWhiteSpace(auth.SessionKey) || auth.SessionKey.Length > 256) throw new ArgumentException("The auth session key is invalid.", nameof(auth));
            if (!Enum.IsDefined(typeof(BackendWireRepresentation), representation)) throw new ArgumentOutOfRangeException(nameof(representation));
            this.appId = appId;
            this.accountId = accountId;
            this.installationId = installationId;
            mediaType = representation == BackendWireRepresentation.MessagePack ? PrivateSyncHttpProvider.MessagePackMediaType : PrivateSyncHttpProvider.DiagnosticJsonMediaType;
        }

        public async Task<RemoteResult<RemoteCommandReceipt>> LookupAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var fingerprint = command.Fingerprint;
            if (command.FingerprintVersion != 1 || fingerprint.Length != 32) return ProtocolFailure();

            byte[] body;
            try
            {
                body = codec.Encode(new RecoveryReceiptRequest(
                    command.StreamId.Value,
                    command.OperationId.Value,
                    installationId,
                    command.Sequence,
                    Hex(fingerprint)));
            }
            catch { return ProtocolFailure(); }
            if (body.Length == 0 || body.Length > 262_144) return ProtocolFailure();

            var attempt = await SendAuthenticatedAsync(body, cancellationToken).ConfigureAwait(false);
            if (attempt.Failure.HasValue) return RemoteResult<RemoteCommandReceipt>.Failed(attempt.Failure.Value);
            var response = attempt.Response!;
            if (response.StatusCode != 200) return RemoteResult<RemoteCommandReceipt>.Failed(DecodeFailure(response));
            if (!ValidProtocolResponse(response)) return ProtocolFailure(response.StatusCode);

            RecoveryReceiptResponse value;
            try { value = codec.Decode<RecoveryReceiptResponse>(response.CopyBody()); }
            catch { return ProtocolFailure(response.StatusCode); }
            try
            {
                if (value == null || value.ProtocolVersion != 1 || value.ClientStreamId != command.StreamId.Value ||
                    value.OperationId != command.OperationId.Value || value.ServerTime < 0 || value.ServerTime > 253_402_300_799_999L ||
                    value.ObservedFinalizedThrough < 0)
                    return ProtocolFailure(response.StatusCode);

                if (!value.Found)
                {
                    if (value.Result != null || value.ObservedFinalizedThrough >= command.Sequence) return ProtocolFailure(response.StatusCode);
                    return RemoteResult<RemoteCommandReceipt>.Success(new RemoteCommandReceipt(false, null, value.ObservedFinalizedThrough));
                }

                if (value.Result == null || value.ObservedFinalizedThrough < command.Sequence) return ProtocolFailure(response.StatusCode);
                RemoteCommandStatus status;
                switch (value.Result)
                {
                    case PushAccepted accepted when accepted.OperationId == command.OperationId.Value && accepted.Sequence == command.Sequence && accepted.FeedRevision >= 0:
                        status = RemoteCommandStatus.Accepted;
                        break;
                    case PushTerminalRejected rejected when rejected.OperationId == command.OperationId.Value && rejected.Sequence == command.Sequence && !rejected.Error.Retryable:
                        status = RemoteCommandStatus.TerminalRejected;
                        break;
                    default:
                        return ProtocolFailure(response.StatusCode);
                }

                byte[] terminalResult;
                try { terminalResult = codec.Encode(value.Result); }
                catch { return ProtocolFailure(response.StatusCode); }
                if (terminalResult.Length == 0 || terminalResult.Length > 65_536) return ProtocolFailure(response.StatusCode);
                var outcome = new RemoteCommandOutcome(status, terminalResult);
                return RemoteResult<RemoteCommandReceipt>.Success(new RemoteCommandReceipt(true, outcome, value.ObservedFinalizedThrough));
            }
            catch { return ProtocolFailure(response.StatusCode); }
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
                ["Accept"] = mediaType,
                ["Content-Type"] = mediaType
            };
            var request = new HttpRequestData("POST", configuration.Resolve("v1/apps/" + appId + "/sync/receipts/lookup").AbsoluteUri, headers, body);
            try { return new Attempt(await executor.SendAsync(request, cancellationToken).ConfigureAwait(false)); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled)); }
            catch (HttpExecutionException error) { return new Attempt(new RemoteFailure(error.Certainty == HttpDeliveryCertainty.NotSent ? RemoteFailureKind.NotSent : RemoteFailureKind.Dependency)); }
            catch { return new Attempt(new RemoteFailure(RemoteFailureKind.Dependency)); }
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
                    !response.TryGetHeader("X-Correlation-Id", out var correlation) || !string.Equals(error.CorrelationId, correlation, StringComparison.Ordinal))
                    return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode);
                return new RemoteFailure(MapCategory(error.Error.Category), response.StatusCode, RetryAfter(response));
            }
            catch { return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode); }
        }

        private bool ValidProtocolResponse(HttpResponseData response) =>
            response.BodyLength > 0 && response.BodyLength <= configuration.MaximumResponseBytes && HasProtocolMediaType(response) &&
            (!response.TryGetHeader("Content-Encoding", out var encoding) || string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase)) &&
            response.TryGetHeader("X-Correlation-Id", out var correlation) && !string.IsNullOrWhiteSpace(correlation) && correlation.Length <= 128;

        private bool HasProtocolMediaType(HttpResponseData response) =>
            response.TryGetHeader("Content-Type", out var contentType) && string.Equals(contentType, mediaType, StringComparison.OrdinalIgnoreCase);

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

        private static string Hex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (var i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = alphabet[bytes[i] >> 4];
                chars[i * 2 + 1] = alphabet[bytes[i] & 15];
            }
            return new string(chars);
        }

        private static RemoteResult<RemoteCommandReceipt> ProtocolFailure(int? status = null) =>
            RemoteResult<RemoteCommandReceipt>.Failed(new RemoteFailure(RemoteFailureKind.Protocol, status));

        private readonly struct Attempt
        {
            public Attempt(HttpResponseData response) { Response = response; Failure = null; }
            public Attempt(RemoteFailure failure) { Response = null; Failure = failure; }
            public HttpResponseData? Response { get; }
            public RemoteFailure? Failure { get; }
        }
    }
}
