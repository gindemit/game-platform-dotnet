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
    public enum BackendWireRepresentation { MessagePack, DiagnosticJson }

    /// <summary>
    /// Portable semantic bootstrap/pull provider. The injected codec and executor
    /// remain consumer-owned; this type does not register a production transport.
    /// </summary>
    public sealed class PrivateSyncHttpProvider : IPrivateSyncRemote
    {
        public const string MessagePackMediaType = "application/vnd.gindemit.platform.v1+msgpack";
        public const string DiagnosticJsonMediaType = "application/vnd.gindemit.platform.v1+json";

        private readonly AppId appId;
        private readonly PlatformUserId accountId;
        private readonly BackendHttpConfiguration configuration;
        private readonly IHttpExecutor executor;
        private readonly IWireCodec codec;
        private readonly IAuthSession auth;
        private readonly string mediaType;
        private readonly AuthRefreshCoordinator refresh;

        public PrivateSyncHttpProvider(
            AppId appId,
            PlatformUserId accountId,
            BackendHttpConfiguration configuration,
            IHttpExecutor executor,
            IWireCodec codec,
            IAuthSession auth,
            AuthRefreshCoordinator refresh,
            BackendWireRepresentation representation = BackendWireRepresentation.MessagePack)
        {
            if (!appId.IsValid) throw new ArgumentException("A valid app ID is required.", nameof(appId));
            if (!accountId.IsValid) throw new ArgumentException("A valid account ID is required.", nameof(accountId));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
            this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            if (string.IsNullOrWhiteSpace(auth.SessionKey) || auth.SessionKey.Length > 256) throw new ArgumentException("The auth session key is invalid.", nameof(auth));
            if (!Enum.IsDefined(typeof(BackendWireRepresentation), representation)) throw new ArgumentOutOfRangeException(nameof(representation));
            this.appId = appId;
            this.accountId = accountId;
            mediaType = representation == BackendWireRepresentation.MessagePack ? MessagePackMediaType : DiagnosticJsonMediaType;
        }

        public async Task<RemoteResult<BootstrapStart>> StartBootstrapAsync(ClientStreamId streamId, CancellationToken cancellationToken)
        {
            if (!streamId.IsValid) throw new ArgumentException("A valid stream ID is required.", nameof(streamId));
            var path = "v1/apps/" + appId + "/bootstrap?clientStreamId=" + streamId.Value.ToString("D", CultureInfo.InvariantCulture);
            var response = await SendAuthenticatedAsync("GET", path, Array.Empty<byte>(), false, cancellationToken).ConfigureAwait(false);
            if (response.Failure.HasValue) return RemoteResult<BootstrapStart>.Failed(response.Failure.Value);
            if (response.Response!.StatusCode != 200) return RemoteResult<BootstrapStart>.Failed(DecodeFailure(response.Response, configuration.MaximumResponseBytes));
            var decoded = Decode<BootstrapStartResponse>(response.Response, configuration.MaximumResponseBytes);
            if (!decoded.IsSuccess) return RemoteResult<BootstrapStart>.Failed(decoded.Failure);
            try
            {
                var value = decoded.Value!;
                if (value.ProtocolVersion != 1 || value.Account.PlatformUserId != accountId.Value || value.Membership.AppId != appId.Value || value.StreamState.ClientStreamId != streamId.Value)
                    return ProtocolFailure<BootstrapStart>(response.Response.StatusCode);
                var collections = new SnapshotCollection[value.Collections.Count];
                for (var i = 0; i < collections.Length; i++)
                    collections[i] = new SnapshotCollection(value.Collections[i].Name, value.Collections[i].SchemaVersion, value.Collections[i].Required);
                return RemoteResult<BootstrapStart>.Success(new BootstrapStart(
                    new ClientStreamId(value.StreamState.ClientStreamId), value.StreamState.State,
                    value.StreamState.FinalizedThrough, value.StreamState.NextSequence,
                    value.CommittedThrough, value.VisibilityGeneration, value.LogEpoch, collections,
                    DecodeToken(value.SnapshotSession), DecodeToken(value.FirstPageToken), value.ExpiresAt, value.ServerTime));
            }
            catch { return ProtocolFailure<BootstrapStart>(response.Response.StatusCode); }
        }

        public async Task<RemoteResult<BootstrapPage>> GetBootstrapPageAsync(byte[] session, byte[] pageToken, int maximumBytes, CancellationToken cancellationToken)
        {
            ValidateToken(session, nameof(session));
            ValidateToken(pageToken, nameof(pageToken));
            if (maximumBytes < 65_536 || maximumBytes > 262_144) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            byte[] body;
            try { body = codec.Encode(new BootstrapPageRequest(EncodeToken(session), EncodeToken(pageToken), maximumBytes)); }
            catch { return ProtocolFailure<BootstrapPage>(); }
            if (body.Length == 0 || body.Length > 262_144) return ProtocolFailure<BootstrapPage>();
            var response = await SendAuthenticatedAsync("POST", "v1/apps/" + appId + "/bootstrap/pages", body, true, cancellationToken).ConfigureAwait(false);
            if (response.Failure.HasValue) return RemoteResult<BootstrapPage>.Failed(response.Failure.Value);
            var responseLimit = Math.Min(configuration.MaximumResponseBytes, maximumBytes);
            if (response.Response!.StatusCode != 200) return RemoteResult<BootstrapPage>.Failed(DecodeFailure(response.Response, responseLimit));
            var decoded = Decode<BootstrapPageResponse>(response.Response, responseLimit);
            if (!decoded.IsSuccess) return RemoteResult<BootstrapPage>.Failed(decoded.Failure);
            try
            {
                var value = decoded.Value!;
                var decodedSession = DecodeToken(value.SnapshotSession);
                if (value.ProtocolVersion != 1 || !Equal(session, decodedSession)) return ProtocolFailure<BootstrapPage>(response.Response.StatusCode);
                var entities = new RemoteProjectionMutation[value.Entities.Count];
                for (var i = 0; i < entities.Length; i++) entities[i] = Snapshot(value.Entities[i]);
                var next = value.NextPageToken == null ? null : DecodeToken(value.NextPageToken);
                var cursor = value.InitialPullCursor == null ? null : DecodeToken(value.InitialPullCursor);
                return RemoteResult<BootstrapPage>.Success(new BootstrapPage(decodedSession, value.CommittedThrough, entities, value.HasMore, next, cursor, value.ServerTime));
            }
            catch { return ProtocolFailure<BootstrapPage>(response.Response.StatusCode); }
        }

        public async Task<RemoteResult<RemotePullPage>> PullAsync(byte[] cursor, int maximumBytes, CancellationToken cancellationToken)
        {
            ValidateToken(cursor, nameof(cursor));
            if (maximumBytes < 131_072 || maximumBytes > 262_144) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            byte[] body;
            try { body = codec.Encode(new PullRequest(EncodeToken(cursor), maximumBytes)); }
            catch { return ProtocolFailure<RemotePullPage>(); }
            if (body.Length == 0 || body.Length > 262_144) return ProtocolFailure<RemotePullPage>();
            var response = await SendAuthenticatedAsync("POST", "v1/apps/" + appId + "/sync/pull", body, true, cancellationToken).ConfigureAwait(false);
            if (response.Failure.HasValue) return RemoteResult<RemotePullPage>.Failed(response.Failure.Value);
            var responseLimit = Math.Min(configuration.MaximumResponseBytes, maximumBytes);
            if (response.Response!.StatusCode != 200) return RemoteResult<RemotePullPage>.Failed(DecodeFailure(response.Response, responseLimit));
            var decoded = Decode<IPullResponse>(response.Response, responseLimit);
            if (!decoded.IsSuccess) return RemoteResult<RemotePullPage>.Failed(decoded.Failure);
            try
            {
                if (decoded.Value is PullReset reset)
                {
                    if (!reset.ResetRequired || reset.Changes.Count != 0 || reset.NextCursor != null || reset.HasMore || !IsResetReason(reset.Reason)) return ProtocolFailure<RemotePullPage>(response.Response.StatusCode);
                    return RemoteResult<RemotePullPage>.Success(RemotePullPage.Reset(reset.Reason, reset.ServerTime));
                }
                if (decoded.Value is PullPage page)
                {
                    if (page.ProtocolVersion != 1 || page.ResetRequired) return ProtocolFailure<RemotePullPage>(response.Response.StatusCode);
                    var groups = new RemotePullGroup[page.Changes.Count];
                    for (var i = 0; i < groups.Length; i++)
                    {
                        var source = page.Changes[i];
                        var changes = new RemoteProjectionMutation[source.Changes.Count];
                        for (var j = 0; j < changes.Length; j++) changes[j] = Change(source.Changes[j]);
                        groups[i] = new RemotePullGroup(source.FeedRevision, changes);
                    }
                    return RemoteResult<RemotePullPage>.Success(RemotePullPage.Page(page.CommittedThrough, groups, DecodeToken(page.NextCursor), page.HasMore, page.ServerTime));
                }
                return ProtocolFailure<RemotePullPage>(response.Response.StatusCode);
            }
            catch { return ProtocolFailure<RemotePullPage>(response.Response.StatusCode); }
        }

        private async Task<Attempt> SendAuthenticatedAsync(string method, string path, byte[] body, bool hasBody, CancellationToken cancellationToken)
        {
            AccessTokenSnapshot token;
            try { token = await auth.GetAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled)); }
            catch { return new Attempt(new RemoteFailure(RemoteFailureKind.Authentication)); }

            var response = await SendOnce(method, path, body, hasBody, token, cancellationToken).ConfigureAwait(false);
            if (response.Failure.HasValue || response.Response!.StatusCode != 401) return response;
            try
            {
                token = await refresh.RefreshAsync(auth, accountId, token.Generation, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled)); }
            catch { return new Attempt(new RemoteFailure(RemoteFailureKind.Authentication, 401)); }
            return await SendOnce(method, path, body, hasBody, token, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Attempt> SendOnce(string method, string path, byte[] body, bool hasBody, AccessTokenSnapshot token, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer " + token.Value,
                ["Accept"] = mediaType
            };
            if (hasBody) headers["Content-Type"] = mediaType;
            var request = new HttpRequestData(method, configuration.Resolve(path).AbsoluteUri, headers, body);
            try { return new Attempt(await executor.SendAsync(request, cancellationToken).ConfigureAwait(false)); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new Attempt(new RemoteFailure(RemoteFailureKind.Cancelled)); }
            catch (HttpExecutionException error) { return new Attempt(new RemoteFailure(error.Certainty == HttpDeliveryCertainty.NotSent ? RemoteFailureKind.NotSent : RemoteFailureKind.Dependency)); }
            catch { return new Attempt(new RemoteFailure(RemoteFailureKind.Dependency)); }
        }

        private RemoteResult<T> Decode<T>(HttpResponseData response, int responseLimit)
        {
            if (!ValidProtocolResponse(response, responseLimit)) return ProtocolFailure<T>(response.StatusCode);
            try { return RemoteResult<T>.Success(codec.Decode<T>(response.CopyBody())); }
            catch { return ProtocolFailure<T>(response.StatusCode); }
        }

        private RemoteFailure DecodeFailure(HttpResponseData response, int responseLimit)
        {
            if (response.BodyLength > responseLimit) return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode);
            var mapped = MapStatus(response);
            if (!HasProtocolMediaType(response)) return mapped;
            if (!ValidProtocolResponse(response, responseLimit)) return new RemoteFailure(RemoteFailureKind.Protocol, response.StatusCode);
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

        private bool ValidProtocolResponse(HttpResponseData response, int responseLimit) =>
            response.BodyLength > 0 && response.BodyLength <= responseLimit &&
            HasProtocolMediaType(response) &&
            (!response.TryGetHeader("Content-Encoding", out var encoding) || string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase)) &&
            response.TryGetHeader("X-Correlation-Id", out var correlation) && !string.IsNullOrWhiteSpace(correlation) && correlation.Length <= 128;

        private bool HasProtocolMediaType(HttpResponseData response) =>
            response.TryGetHeader("Content-Type", out var contentType) && string.Equals(contentType, mediaType, StringComparison.OrdinalIgnoreCase);

        private RemoteFailure MapStatus(HttpResponseData response)
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

        private static bool IsResetReason(string reason) => reason == "history_expired" || reason == "visibility_changed" ||
            reason == "log_epoch_changed" || reason == "cursor_invalidated";

        private static TimeSpan? RetryAfter(HttpResponseData response)
        {
            if (response.TryGetHeader("Retry-After", out var value) && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 && seconds <= 86_400)
                return TimeSpan.FromSeconds(seconds);
            return null;
        }

        private RemoteProjectionMutation Snapshot(IProjectionSnapshotEntity value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return value switch
            {
                ProjectionProfileSnapshot item => Upsert(item.Collection, item.EntityKey.Profile, item.Revision, value),
                ProjectionProgressionSnapshot item => Upsert(item.Collection, item.EntityKey.StateKey, item.Revision, value),
                ProjectionInventorySnapshot item => Upsert(item.Collection, item.EntityKey.ItemId, item.Revision, value),
                ProjectionWalletSnapshot item => Upsert(item.Collection, item.EntityKey.CurrencyId, item.Revision, value),
                ProjectionEntitlementSnapshot item => Upsert(item.Collection, item.EntityKey.EntitlementId, item.Revision, value),
                _ => throw new InvalidOperationException("Unknown snapshot entity type.")
            };
        }

        private RemoteProjectionMutation Change(IProjectionChange value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return value switch
            {
                ProjectionProfileUpsert item => Upsert(item.EntityType, item.EntityKey.Profile, item.Revision, value),
                ProjectionProgressionUpsert item => Upsert(item.EntityType, item.EntityKey.StateKey, item.Revision, value),
                ProjectionInventoryUpsert item => Upsert(item.EntityType, item.EntityKey.ItemId, item.Revision, value),
                ProjectionWalletUpsert item => Upsert(item.EntityType, item.EntityKey.CurrencyId, item.Revision, value),
                ProjectionEntitlementUpsert item => Upsert(item.EntityType, item.EntityKey.EntitlementId, item.Revision, value),
                ProjectionProfileRemovalValue item => Removal(item.EntityType, item.EntityKey.Profile, item.Revision, item.Kind),
                ProjectionProgressionRemoval item => Removal(item.EntityType, item.EntityKey.StateKey, item.Revision, item.Kind),
                ProjectionInventoryRemoval item => Removal(item.EntityType, item.EntityKey.ItemId, item.Revision, item.Kind),
                ProjectionWalletRemoval item => Removal(item.EntityType, item.EntityKey.CurrencyId, item.Revision, item.Kind),
                ProjectionEntitlementRemoval item => Removal(item.EntityType, item.EntityKey.EntitlementId, item.Revision, item.Kind),
                _ => throw new InvalidOperationException("Unknown projection change type.")
            };
        }

        private RemoteProjectionMutation Upsert(string collection, string key, long revision, IProjectionSnapshotEntity value) =>
            EncodedUpsert(collection, key, revision, codec.Encode<IProjectionSnapshotEntity>(value));
        private RemoteProjectionMutation Upsert(string collection, string key, long revision, IProjectionChange value) =>
            EncodedUpsert(collection, key, revision, codec.Encode<IProjectionChange>(value));
        private static RemoteProjectionMutation EncodedUpsert(string collection, string key, long revision, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > 262_144) throw new InvalidOperationException("Projection bytes are invalid.");
            return new RemoteProjectionMutation(collection, key, revision, ProjectionMutationKind.Upsert, bytes);
        }
        private static RemoteProjectionMutation Removal(string collection, string key, long revision, string kind) =>
            new RemoteProjectionMutation(collection, key, revision,
                kind == "view_remove" ? ProjectionMutationKind.RemoveFromView : kind == "tombstone" ? ProjectionMutationKind.Tombstone : throw new InvalidOperationException("Unknown removal kind."), null);

        private static string EncodeToken(byte[] value) => Convert.ToBase64String((byte[])value.Clone()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static byte[] DecodeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.IndexOf('=') >= 0) throw new FormatException("Opaque token is not canonical base64url.");
            var bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
            if (!string.Equals(EncodeToken(bytes), value, StringComparison.Ordinal)) throw new FormatException("Opaque token is not canonical base64url.");
            ValidateToken(bytes, nameof(value));
            return bytes;
        }
        private static void ValidateToken(byte[] value, string name)
        {
            if (value == null || value.Length < 12 || value.Length > 3072) throw new ArgumentOutOfRangeException(name);
        }
        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            var difference = 0;
            for (var i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }
        private static RemoteResult<T> ProtocolFailure<T>(int? status = null) => RemoteResult<T>.Failed(new RemoteFailure(RemoteFailureKind.Protocol, status));

        private readonly struct Attempt
        {
            public Attempt(HttpResponseData response) { Response = response; Failure = null; }
            public Attempt(RemoteFailure failure) { Response = null; Failure = failure; }
            public HttpResponseData? Response { get; }
            public RemoteFailure? Failure { get; }
        }
    }
}
