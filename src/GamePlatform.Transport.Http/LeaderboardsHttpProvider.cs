#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Leaderboards;
using GamePlatform.Serialization.MessagePack;
using GamePlatform.Transport.Abstractions;
using GamePlatform.Wire.Contracts;
using W = GamePlatform.Wire.Contracts.Leaderboards;

namespace GamePlatform.Transport.Http
{
    /// <summary>Authenticated Leaderboards provider and closed durable codec. Requests never fall back to JSON.</summary>
    public sealed class LeaderboardsHttpProvider : ILeaderboardsRemote
    {
        private readonly AppId appId;
        private readonly PlatformUserId accountId;
        private readonly BackendHttpConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly IAuthSession auth;
        private readonly AuthRefreshCoordinator refresh;
        private readonly LeaderboardsMessagePackCodec codec = new LeaderboardsMessagePackCodec();
        private readonly FriendsMessagePackCodec friendsCodec = new FriendsMessagePackCodec();
        private readonly MessagePackWireCodec errors = new MessagePackWireCodec();

        public LeaderboardsHttpProvider(AppId appId, PlatformUserId accountId, BackendHttpConfiguration configuration,
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

        public Task<RemoteResult<LeaderboardsResponse>> QueryAsync(LeaderboardsQuery query, CancellationToken cancellationToken)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            if (query.Scope == "friends")
                return SendAsync("friends/query", friendsCodec.EncodeRankingQuery(query.BoardId, query.Limit, query.Cursor), false,
                bytes =>
                {
                    var value = LeaderboardsModelMapper.ToDomain(friendsCodec.DecodeRankingResponse(bytes));
                    if (value.AppId != appId.Value || value.AccountId != accountId.Value || value.BoardId != query.BoardId || value.Entries.Count > query.Limit)
                        throw new ArgumentException("Response friends context mismatch.");
                    return value;
                }, cancellationToken);
            return SendAsync("query", codec.EncodeQuery(LeaderboardsModelMapper.ToWire(query)), false,
                bytes =>
                {
                    var value = LeaderboardsModelMapper.ToDomain(codec.DecodeResponse(bytes));
                    if (value.BoardId != query.BoardId || value.Scope != query.Scope || value.CountryCode != query.CountryCode || value.Entries.Count > query.Limit)
                        throw new ArgumentException("Response query mismatch.");
                    return value;
                }, cancellationToken);
        }
        public Task<RemoteResult<LeaderboardsCountryResponse>> SetCountryAsync(LeaderboardsCountryCommand command, CancellationToken cancellationToken)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            return SendAsync("country", codec.EncodeCountry(LeaderboardsModelMapper.ToWire(command)), true,
                bytes =>
                {
                    var value = LeaderboardsModelMapper.ToDomain(codec.DecodeCountryResponse(bytes));
                    if (value.OperationId != command.OperationId.Value || value.BoardId != command.BoardId || value.ParticipantKind != command.ParticipantKind ||
                        value.CountryCode != command.CountryCode || command.TeamId.HasValue && command.TeamId.Value != value.ParticipantId)
                        throw new ArgumentException("Response country mismatch.");
                    return value;
                }, cancellationToken);
        }

        private async Task<RemoteResult<T>> SendAsync<T>(string route, byte[] body, bool mutation, Func<byte[], T> decode, CancellationToken cancellationToken) where T : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            AccessTokenSnapshot token;
            try
            {
                token = await auth.GetAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Failure<T>(RemoteFailureKind.Cancelled); }
            catch { return Failure<T>(RemoteFailureKind.Authentication); }
            var attempt = await SendOnceAsync(route, body, token, mutation, cancellationToken).ConfigureAwait(false);
            if (attempt.Failure.HasValue)
                return RemoteResult<T>.Failed(attempt.Failure.Value);
            if (attempt.Response!.StatusCode == 401)
            {
                try
                {
                    token = await refresh.RefreshAsync(auth, accountId, token.Generation, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Failure<T>(RemoteFailureKind.Cancelled); }
                catch { return Failure<T>(RemoteFailureKind.Authentication, 401); }
                attempt = await SendOnceAsync(route, body, token, mutation, cancellationToken).ConfigureAwait(false);
                if (attempt.Failure.HasValue)
                    return RemoteResult<T>.Failed(attempt.Failure.Value);
            }
            var response = attempt.Response!;
            if (response.StatusCode != 200)
                throw DecodeFailure(response);
            if (!ValidResponse(response))
                return Failure<T>(RemoteFailureKind.Protocol, response.StatusCode);
            try
            {
                var value = decode(response.CopyBody());
                return RemoteResult<T>.Success(value);
            }
            catch { return Failure<T>(RemoteFailureKind.Protocol, response.StatusCode); }
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
                var response = await executor.SendAsync(new HttpRequestData("POST", configuration.Resolve("v1/apps/" + appId + "/leaderboards/" + route).AbsoluteUri, headers, body), cancellationToken).ConfigureAwait(false);
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

        private LeaderboardsOperationException DecodeFailure(HttpResponseData response)
        {
            if (!ValidResponse(response))
                return new LeaderboardsOperationException(new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode), "leaderboards_protocol");
            var kind = response.StatusCode == 401 ? RemoteFailureKind.Authentication : response.StatusCode == 403 ? RemoteFailureKind.Authorization :
                response.StatusCode == 409 ? RemoteFailureKind.Conflict : response.StatusCode == 429 ? RemoteFailureKind.RateLimited :
                response.StatusCode == 404 ? RemoteFailureKind.Unavailable : response.StatusCode >= 500 ? RemoteFailureKind.Dependency : RemoteFailureKind.Validation;
            TimeSpan? retry = null;
            if (response.TryGetHeader("Retry-After", out var retryText) && int.TryParse(retryText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 && seconds <= 86400)
                retry = TimeSpan.FromSeconds(seconds);
            string code = "leaderboards_unavailable";
            if (ValidResponse(response))
            {
                try
                {
                    var error = errors.Decode<ErrorResponse>(response.CopyBody());
                    if (error.CorrelationId != response.Headers["X-Correlation-Id"])
                        return new LeaderboardsOperationException(new RemoteFailure(RemoteFailureKind.Protocol), "leaderboards_protocol");
                    code = error.Error.Code;
                }
                catch { return new LeaderboardsOperationException(new RemoteFailure(RemoteFailureKind.Protocol), "leaderboards_protocol"); }
            }
            return new LeaderboardsOperationException(new RemoteFailure(kind, response.StatusCode, retry), code);
        }
        private static RemoteResult<T> Failure<T>(RemoteFailureKind kind, int? status = null) where T : class => RemoteResult<T>.Failed(new RemoteFailure(kind, status));
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
