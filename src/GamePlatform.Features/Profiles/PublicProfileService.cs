using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Backend.Contracts.Remote;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;

namespace GamePlatform.Features.Profiles
{
    /// <summary>
    /// Account-scoped reads of another player's public profile with a durable last-confirmed cache.
    /// </summary>
    public sealed class PublicProfileService : IDisposable
    {
        private const string Namespace = "profiles.public";
        private readonly ScopedOwnerContext owner;
        private readonly StorageScope storageScope;
        private readonly IPublicProfileRemote? remote;
        private readonly IPublicProfileStateCodec codec;
        private readonly IDurableFeatureStateStore state;
        private readonly ISerializedStorageExecutor transactions;
        private readonly Func<long> nowMilliseconds;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly object gate = new object();
        private bool disposed;

        public PublicProfileService(ScopedOwnerContext owner, IPublicProfileRemote? remote, IPublicProfileStateCodec codec,
            IDurableFeatureStateStore state, ISerializedStorageExecutor transactions, Func<long> nowMilliseconds)
        {
            if (!owner.IsValid)
                throw new ArgumentException("A captured owner is required.", nameof(owner));
            this.owner = owner;
            storageScope = new StorageScope(owner.Owner.Backend.Value, new PlatformId(owner.Owner.AppId.ToString()), new PlatformId(owner.Owner.UserId.ToString()));
            this.remote = remote;
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            this.nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
        }

        public async Task<FeatureSnapshot<PublicProfileResponse>> ReadCachedAsync(ScopedOwnerContext requestedOwner, PublicProfileQuery query, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner);
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            using var linked = Link(cancellationToken);
            var cached = await state.ReadAsync(owner, Namespace, Key(query), linked.Token).ConfigureAwait(false);
            EnsureOwner(requestedOwner);
            linked.Token.ThrowIfCancellationRequested();
            if (cached == null)
                return new FeatureSnapshot<PublicProfileResponse>(owner, 0, SnapshotFreshness.Missing, null);
            var value = codec.DecodeResponse(cached.CopyPayload());
            Validate(query, value);
            return Snapshot(value, SnapshotFreshness.Stale);
        }

        public async Task<FeatureSnapshot<PublicProfileResponse>> QueryAsync(ScopedOwnerContext requestedOwner, PublicProfileQuery query, CancellationToken cancellationToken)
        {
            EnsureOwner(requestedOwner);
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            if (remote == null)
                throw Error(RemoteFailureKind.Unavailable, "profiles_public_offline");
            using var linked = Link(cancellationToken);
            RemoteResult<PublicProfileResponse> result;
            try
            {
                result = await remote.QueryAsync(query, linked.Token).ConfigureAwait(false);
            }
            catch (PublicProfileOperationException error)
            {
                if (error.Failure.Kind == RemoteFailureKind.Authorization)
                    await ForgetAsync(query, linked.Token).ConfigureAwait(false);
                throw;
            }
            EnsureOwner(requestedOwner);
            linked.Token.ThrowIfCancellationRequested();
            if (!result.IsSuccess)
            {
                if (result.Failure.Kind == RemoteFailureKind.Authorization)
                    await ForgetAsync(query, linked.Token).ConfigureAwait(false);
                throw new PublicProfileOperationException(result.Failure, "profiles_public_" + result.Failure.Kind.ToString().ToLowerInvariant());
            }
            var value = result.Value!;
            Validate(query, value);
            var bytes = codec.EncodeResponse(value);
            await transactions.ExecuteAsync(storageScope, tx =>
            {
                lock (gate)
                {
                    EnsureOwner(requestedOwner);
                    linked.Token.ThrowIfCancellationRequested();
                    var prior = state.Read(tx, owner, Namespace, Key(query));
                    state.Upsert(tx, new DurableFeatureMutation(owner, Namespace, Key(query), checked((prior?.Revision ?? 0) + 1), Now(), bytes, Array.Empty<byte>()));
                    return true;
                }
            }, linked.Token).ConfigureAwait(false);
            EnsureOwner(requestedOwner);
            linked.Token.ThrowIfCancellationRequested();
            return Snapshot(value, SnapshotFreshness.Current);
        }

        private Task<bool> ForgetAsync(PublicProfileQuery query, CancellationToken token) => transactions.ExecuteAsync(storageScope, tx =>
        {
            lock (gate)
            {
                EnsureOwner(owner);
                token.ThrowIfCancellationRequested();
                state.Delete(tx, owner, Namespace, Key(query));
                return true;
            }
        }, token);

        private void Validate(PublicProfileQuery query, PublicProfileResponse value)
        {
            if (value == null || value.AppId != owner.Owner.AppId.Value || value.PlayerId != query.SubjectPlayerId)
                throw Error(RemoteFailureKind.Protocol, "profiles_public_mismatch");
        }

        private static string Key(PublicProfileQuery query) => query.SubjectPlayerId.ToString("D");

        private static PublicProfileOperationException Error(RemoteFailureKind kind, string code) => new PublicProfileOperationException(new RemoteFailure(kind), code);

        private long Now()
        {
            long value = nowMilliseconds();
            ProfileValidation.Timestamp(value, nameof(nowMilliseconds));
            return value;
        }

        private FeatureSnapshot<PublicProfileResponse> Snapshot(PublicProfileResponse value, SnapshotFreshness freshness) =>
            new FeatureSnapshot<PublicProfileResponse>(owner, 0, freshness == SnapshotFreshness.Current ? FeatureSnapshotState.Available : FeatureSnapshotState.Stale, freshness, value, null, null);

        private CancellationTokenSource Link(CancellationToken token)
        {
            lock (gate)
            {
                EnsureOwner(owner);
                return CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            }
        }

        private void EnsureOwner(ScopedOwnerContext value)
        {
            lock (gate)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(PublicProfileService));
                if (value != owner)
                    throw Error(RemoteFailureKind.Authorization, "profiles_public_owner_mismatch");
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                    return;
                disposed = true;
            }
            lifetime.Cancel();
        }
    }
}
