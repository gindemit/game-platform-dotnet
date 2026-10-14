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
    /// <summary>Account and installation-bound MessagePack stream inspection and recovery.</summary>
    public sealed class CommandStreamRecoveryHttpProvider : ICommandStreamRecoveryRemote
    {
        private readonly AppId appId;
        private readonly PlatformUserId accountId;
        private readonly Guid installationId;
        private readonly BackendHttpConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly IWireCodec codec;
        private readonly IAuthSession auth;
        private readonly AuthRefreshCoordinator refresh;

        public CommandStreamRecoveryHttpProvider(AppId appId, PlatformUserId accountId, Guid installationId,
            BackendHttpConfiguration configuration, IHttpExecutor executor, IWireCodec codec,
            IAuthSession auth, AuthRefreshCoordinator refresh)
        {
            if (!appId.IsValid || !accountId.IsValid || !UuidIdentity.IsValid(installationId))
                throw new ArgumentException("Valid account and installation identities are required.");
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            if (string.IsNullOrWhiteSpace(auth.SessionKey) || auth.SessionKey.Length > 256)
                throw new ArgumentException("The auth session key is invalid.", nameof(auth));
            this.appId = appId;
            this.accountId = accountId;
            this.installationId = installationId;
        }

        public async Task<RemoteResult<CommandStreamInspection>> InspectAsync(ClientStreamId streamId, CancellationToken cancellationToken)
        {
            if (!streamId.IsValid)
                throw new ArgumentException("A valid stream ID is required.", nameof(streamId));
            byte[] body;
            try
            {
                body = codec.Encode(new RecoveryInspectRequest(streamId.Value, installationId));
            }
            catch { return Failure<CommandStreamInspection>(RemoteFailureKind.Protocol); }
            if (body.Length == 0 || body.Length > 262_144)
                return Failure<CommandStreamInspection>(RemoteFailureKind.Protocol);
            var attempt = await SendAuthenticatedAsync("inspect", body, false, cancellationToken).ConfigureAwait(false);
            if (attempt.Failure.HasValue)
                return RemoteResult<CommandStreamInspection>.Failed(attempt.Failure.Value);
            var response = attempt.Response!;
            if (response.StatusCode != 200)
                return RemoteResult<CommandStreamInspection>.Failed(DecodeFailure(response, false));
            if (!ValidProtocolResponse(response))
                return Failure<CommandStreamInspection>(RemoteFailureKind.Protocol, response.StatusCode);
            try
            {
                var value = codec.Decode<RecoveryInspectResponse>(response.CopyBody());
                var state = value.StreamState;
                if (value.ProtocolVersion != 1 || state.ClientStreamId != streamId.Value || state.InstallationId != installationId ||
                    state.FinalizedThrough < 0 || (state.NextSequence.HasValue && state.NextSequence.Value <= 0))
                    return Failure<CommandStreamInspection>(RemoteFailureKind.Protocol, response.StatusCode);
                var status = state.State == "active" ? CommandStreamState.Active :
                    state.State == "retired" ? CommandStreamState.Retired : (CommandStreamState)(-1);
                return RemoteResult<CommandStreamInspection>.Success(new CommandStreamInspection(streamId, installationId,
                    state.FinalizedThrough, state.NextSequence, status, value.InspectionToken, value.ExpiresAt, value.ServerTime));
            }
            catch { return Failure<CommandStreamInspection>(RemoteFailureKind.Protocol, response.StatusCode); }
        }

        public async Task<RemoteResult<CommandStreamRecoveryResult>> RecoverAsync(CommandStreamRecoveryRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            byte[] body;
            try
            {
                body = codec.Encode(new RecoveryStreamRequest(installationId, request.PriorStreamId.Value,
                    request.NewStreamId.Value, ReasonWireValue(request.Reason), request.RecoveryId,
                    request.InspectionToken, request.ExpectedFinalizedThrough));
            }
            catch { return Failure<CommandStreamRecoveryResult>(RemoteFailureKind.Protocol); }
            if (body.Length == 0 || body.Length > 262_144)
                return Failure<CommandStreamRecoveryResult>(RemoteFailureKind.Protocol);
            var attempt = await SendAuthenticatedAsync("recover", body, true, cancellationToken).ConfigureAwait(false);
            if (attempt.Failure.HasValue)
                return RemoteResult<CommandStreamRecoveryResult>.Failed(attempt.Failure.Value);
            var response = attempt.Response!;
            if (response.StatusCode != 200)
                return RemoteResult<CommandStreamRecoveryResult>.Failed(DecodeFailure(response, true));
            if (!ValidProtocolResponse(response))
                return Failure<CommandStreamRecoveryResult>(RemoteFailureKind.Protocol, response.StatusCode);
            try
            {
                var value = codec.Decode<RecoveryStreamResponse>(response.CopyBody());
                if (value.ProtocolVersion != 1 || value.PriorClientStreamId != request.PriorStreamId.Value ||
                    value.NewClientStreamId != request.NewStreamId.Value || value.RecoveryId != request.RecoveryId ||
                    value.NextSequence != 1 || value.PriorFinalizedThrough != request.ExpectedFinalizedThrough)
                    return Failure<CommandStreamRecoveryResult>(RemoteFailureKind.Protocol, response.StatusCode);
                return RemoteResult<CommandStreamRecoveryResult>.Success(new CommandStreamRecoveryResult(
                    request.PriorStreamId, request.NewStreamId, request.RecoveryId, value.PriorFinalizedThrough,
                    value.NextSequence, value.ServerTime));
            }
            catch { return Failure<CommandStreamRecoveryResult>(RemoteFailureKind.Protocol, response.StatusCode); }
        }

        private async Task<Attempt> SendAuthenticatedAsync(string operation, byte[] body, bool changesState, CancellationToken cancellationToken)
        {
            AccessTokenSnapshot token;
            try
            {
                token = await auth.GetAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled)); }
            catch { return new Attempt(new RemoteFailure(RemoteFailureKind.Authentication)); }
            var attempt = await SendOnce(operation, body, token, changesState, cancellationToken).ConfigureAwait(false);
            if (attempt.Failure.HasValue || attempt.Response!.StatusCode != 401)
                return attempt;
            try
            {
                token = await refresh.RefreshAsync(auth, accountId, token.Generation, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled)); }
            catch { return new Attempt(new RemoteFailure(RemoteFailureKind.Authentication, 401)); }
            return await SendOnce(operation, body, token, changesState, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Attempt> SendOnce(string operation, byte[] body, AccessTokenSnapshot token,
            bool changesState, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer " + token.Value,
                ["Accept"] = PrivateSyncHttpProvider.MessagePackMediaType,
                ["Content-Type"] = PrivateSyncHttpProvider.MessagePackMediaType
            };
            var request = new HttpRequestData("POST", configuration.Resolve("v1/apps/" + appId + "/sync/streams/" + operation).AbsoluteUri, headers, body);
            try
            {
                return new Attempt(await executor.SendAsync(request, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new Attempt(new RemoteFailure(changesState ? RemoteFailureKind.OutcomeUncertain : RemoteFailureKind.Cancelled));
            }
            catch (HttpExecutionException error)
            {
                var kind = error.Certainty == HttpDeliveryCertainty.NotSent ? RemoteFailureKind.NotSent :
                    changesState ? RemoteFailureKind.OutcomeUncertain : RemoteFailureKind.Dependency;
                return new Attempt(new RemoteFailure(kind));
            }
            catch { return new Attempt(new RemoteFailure(changesState ? RemoteFailureKind.OutcomeUncertain : RemoteFailureKind.Dependency)); }
        }

        private RemoteFailure DecodeFailure(HttpResponseData response, bool recovery)
        {
            var mapped = MapStatus(response);
            if (response.BodyLength > configuration.MaximumResponseBytes)
                return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode);
            if (!HasProtocolMediaType(response))
                return mapped;
            if (!ValidProtocolResponse(response))
                return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode);
            try
            {
                var error = codec.Decode<ErrorResponse>(response.CopyBody());
                if (error == null || error.ProtocolVersion != 1 || error.Error == null ||
                    !response.TryGetHeader("X-Correlation-Id", out var correlation) ||
                    !string.Equals(error.CorrelationId, correlation, StringComparison.Ordinal))
                    return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode);
                if (recovery && response.StatusCode == 409 && error.Error.Category == "conflict" && error.Error.Code == "inspection_expired")
                    return new RemoteFailure(RemoteFailureKind.InspectionExpired, response.StatusCode);
                return new RemoteFailure(MapCategory(error.Error.Category), response.StatusCode, RetryAfter(response));
            }
            catch { return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode); }
        }

        private bool ValidProtocolResponse(HttpResponseData response) =>
            response.BodyLength > 0 && response.BodyLength <= configuration.MaximumResponseBytes && HasProtocolMediaType(response) &&
            (!response.TryGetHeader("Content-Encoding", out var encoding) || string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase)) &&
            response.TryGetHeader("X-Correlation-Id", out var correlation) && !string.IsNullOrWhiteSpace(correlation) && correlation.Length <= 128;

        private static bool HasProtocolMediaType(HttpResponseData response) =>
            response.TryGetHeader("Content-Type", out var contentType) &&
            string.Equals(contentType, PrivateSyncHttpProvider.MessagePackMediaType, StringComparison.OrdinalIgnoreCase);

        private static string ReasonWireValue(CommandStreamRecoveryReason reason) => reason switch
        {
            CommandStreamRecoveryReason.LocalDatabaseRecreated => "local_database_recreated",
            CommandStreamRecoveryReason.BackupRestored => "backup_restored",
            CommandStreamRecoveryReason.StreamIdentityConflict => "stream_identity_conflict",
            _ => throw new ArgumentOutOfRangeException(nameof(reason))
        };

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
            if (response.TryGetHeader("Retry-After", out var value) && int.TryParse(value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 && seconds <= 86_400)
                return TimeSpan.FromSeconds(seconds);
            return null;
        }

        private static RemoteResult<T> Failure<T>(RemoteFailureKind kind, int? status = null) =>
            RemoteResult<T>.Failed(new RemoteFailure(kind, status));

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
