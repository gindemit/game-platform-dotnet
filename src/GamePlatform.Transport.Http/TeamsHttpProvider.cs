#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Teams;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Wire.Contracts;
using W = GamePlatform.Wire.Contracts.Teams;

namespace GamePlatform.Transport.Http
{
    /// <summary>Authenticated Teams provider and closed durable codec. Requests never fall back to JSON.</summary>
    public sealed class TeamsHttpProvider : ITeamsRemote, ITeamsStateCodec
    {
        private readonly AppId appId;
        private readonly PlatformUserId accountId;
        private readonly BackendHttpConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly IAuthSession auth;
        private readonly AuthRefreshCoordinator refresh;
        private readonly TeamsMessagePackCodec codec = new TeamsMessagePackCodec();
        private readonly MessagePackWireCodec errors = new MessagePackWireCodec();

        public TeamsHttpProvider(AppId appId, PlatformUserId accountId, BackendHttpConfiguration configuration,
            IHttpExecutor executor, IAuthSession auth, AuthRefreshCoordinator refresh)
        {
            if (!appId.IsValid || !accountId.IsValid) throw new ArgumentException("Valid authenticated app/account identities are required.");
            this.appId = appId; this.accountId = accountId;
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            if (string.IsNullOrWhiteSpace(auth.SessionKey) || auth.SessionKey.Length > 256) throw new ArgumentException("Invalid auth session key.", nameof(auth));
        }

        public Task<RemoteResult<TeamsResponse>> QueryAsync(TeamsQuery query, CancellationToken cancellationToken)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return SendAsync("query", codec.EncodeQuery(new W.TeamsQuery(query.View, query.TeamId, query.Search, query.Cursor, query.PageSize)), null, cancellationToken);
        }
        public Task<RemoteResult<TeamsResponse>> ExecuteAsync(TeamsCommand command, CancellationToken cancellationToken)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            return SendAsync("commands", EncodeCommand(command), command.OperationId.Value, cancellationToken);
        }
        public byte[] EncodeResponse(TeamsResponse response) => codec.EncodeResponse(TeamsModelMapper.ToW(response));
        public TeamsResponse DecodeResponse(byte[] bytes) => TeamsModelMapper.ToD(codec.DecodeResponse(bytes));
        public byte[] EncodeCommand(TeamsCommand command)
        {
            var p = command.Payload;
            return codec.EncodeCommand(new W.TeamsCommand(command.OperationId.Value, command.Action, command.TeamId, command.ExpectedRevision,
                new W.TeamsCommandPayload(p.Name, p.Description, p.BadgeId, p.Language, p.JoinPolicy, p.RequiredLevel, p.Capacity, p.TargetPlayerId,
                    p.InviteId, p.MessageId, p.RequestId, p.Role, p.Text, p.Reason, p.DurationMinutes)));
        }
        public TeamsCommand DecodeCommand(byte[] bytes)
        {
            var value = codec.DecodeCommand(bytes); var p = value.Payload;
            return new TeamsCommand(new OperationId(value.OperationId), value.Action, value.TeamId, value.ExpectedRevision,
                new TeamsCommandPayload(p.Name, p.Description, p.BadgeId, p.Language, p.JoinPolicy, p.RequiredLevel, p.Capacity, p.TargetPlayerId,
                    p.InviteId, p.MessageId, p.RequestId, p.Role, p.Text, p.Reason, p.DurationMinutes));
        }

        private async Task<RemoteResult<TeamsResponse>> SendAsync(string route, byte[] body, Guid? operationId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AccessTokenSnapshot token;
            try { token = await auth.GetAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Failure(RemoteFailureKind.Cancelled); }
            catch { return Failure(RemoteFailureKind.Authentication); }
            var attempt = await SendOnceAsync(route, body, token, operationId.HasValue, cancellationToken).ConfigureAwait(false);
            if (attempt.Failure.HasValue) return RemoteResult<TeamsResponse>.Failed(attempt.Failure.Value);
            if (attempt.Response!.StatusCode == 401)
            {
                try { token = await refresh.RefreshAsync(auth, accountId, token.Generation, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Failure(RemoteFailureKind.Cancelled); }
                catch { return Failure(RemoteFailureKind.Authentication, 401); }
                attempt = await SendOnceAsync(route, body, token, operationId.HasValue, cancellationToken).ConfigureAwait(false);
                if (attempt.Failure.HasValue) return RemoteResult<TeamsResponse>.Failed(attempt.Failure.Value);
            }
            var response = attempt.Response!;
            if (response.StatusCode != 200) throw DecodeFailure(response);
            if (!ValidResponse(response)) return Failure(RemoteFailureKind.Protocol, response.StatusCode);
            try
            {
                var value = DecodeResponse(response.CopyBody());
                if (value.AppId != appId.Value || value.AccountId != accountId.Value || value.OperationId != operationId ||
                    value.Membership != null && value.Membership.PlayerId != value.PlayerId)
                    return Failure(RemoteFailureKind.Protocol, response.StatusCode);
                return RemoteResult<TeamsResponse>.Success(value);
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
                var response = await executor.SendAsync(new HttpRequestData("POST", configuration.Resolve("v1/apps/" + appId + "/teams/" + route).AbsoluteUri, headers, body), cancellationToken).ConfigureAwait(false);
                return new Attempt(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { return new Attempt(new RemoteFailure(mutation ? RemoteFailureKind.OutcomeUncertain : RemoteFailureKind.Cancelled)); }
            catch (HttpExecutionException error)
            { return new Attempt(new RemoteFailure(error.Certainty == HttpDeliveryCertainty.NotSent ? RemoteFailureKind.NotSent : mutation ? RemoteFailureKind.OutcomeUncertain : RemoteFailureKind.Dependency)); }
            catch { return new Attempt(new RemoteFailure(mutation ? RemoteFailureKind.OutcomeUncertain : RemoteFailureKind.Dependency)); }
        }

        private bool ValidResponse(HttpResponseData response) => response.BodyLength > 0 && response.BodyLength <= configuration.MaximumResponseBytes &&
            response.TryGetHeader("Content-Type", out var media) && string.Equals(media, PrivateSyncHttpProvider.MessagePackMediaType, StringComparison.OrdinalIgnoreCase) &&
            (!response.TryGetHeader("Content-Encoding", out var encoding) || string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase)) &&
            response.TryGetHeader("X-Correlation-Id", out var correlation) && correlation.Length > 0 && correlation.Length <= 128;

        private TeamsOperationException DecodeFailure(HttpResponseData response)
        {
            if (!ValidResponse(response)) return new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode), "teams_protocol");
            var kind = response.StatusCode == 401 ? RemoteFailureKind.Authentication : response.StatusCode == 403 ? RemoteFailureKind.Authorization :
                response.StatusCode == 409 ? RemoteFailureKind.Conflict : response.StatusCode == 429 ? RemoteFailureKind.RateLimited :
                response.StatusCode == 404 ? RemoteFailureKind.Unavailable : response.StatusCode >= 500 ? RemoteFailureKind.Dependency : RemoteFailureKind.Validation;
            TimeSpan? retry = null;
            if (response.TryGetHeader("Retry-After", out var retryText) && int.TryParse(retryText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 && seconds <= 86400)
                retry = TimeSpan.FromSeconds(seconds);
            string code = "teams_unavailable";
            if (ValidResponse(response))
            {
                try
                {
                    var error = errors.Decode<ErrorResponse>(response.CopyBody());
                    if (error.CorrelationId != response.Headers["X-Correlation-Id"]) return new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Protocol), "teams_protocol");
                    code = error.Error.Code;
                }
                catch { return new TeamsOperationException(new RemoteFailure(RemoteFailureKind.Protocol), "teams_protocol"); }
            }
            return new TeamsOperationException(new RemoteFailure(kind, response.StatusCode, retry), code);
        }
        private static RemoteResult<TeamsResponse> Failure(RemoteFailureKind kind, int? status = null) => RemoteResult<TeamsResponse>.Failed(new RemoteFailure(kind, status));
        private readonly struct Attempt
        {
            public Attempt(HttpResponseData response) { Response = response; Failure = null; }
            public Attempt(RemoteFailure failure) { Response = null; Failure = failure; }
            public HttpResponseData? Response { get; }
            public RemoteFailure? Failure { get; }
        }
    }
}
