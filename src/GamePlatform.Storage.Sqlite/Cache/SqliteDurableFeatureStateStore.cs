#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Sqlite.Executor;

namespace GamePlatform.Storage.Sqlite.Cache
{
    public sealed class SqliteDurableFeatureStateStore : IDurableFeatureStateStore
    {
        private readonly SqliteDatabase database;
        private readonly StorageScope scope;
        public SqliteDurableFeatureStateStore(SqliteDatabase database, StorageScope scope)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            if (!database.Scope.Equals(scope)) throw new StorageException(StorageFailure.InvalidOwner, "The feature-state scope does not match the database.");
            this.scope = scope;
        }

        public Task<DurableFeatureState?> ReadAsync(ScopedOwnerContext owner, string featureNamespace, string entityKey, CancellationToken cancellationToken)
        {
            ValidateIdentity(owner, featureNamespace, entityKey);
            EnsureOwner(owner.Owner);
            return database.ExecuteAsync(scope, transaction =>
            {
                var session = RequireSession(transaction);
                var args = new object[] { owner.ViewKey.Value, featureNamespace, entityKey };
                if (session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args) == 0) return null;
                var row = new SqliteFeatureStateRow(
                    session.ExecuteScalar<string>("SELECT view_key FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args),
                    session.ExecuteScalar<string>("SELECT feature_namespace FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args),
                    session.ExecuteScalar<string>("SELECT entity_key FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args),
                    session.ExecuteScalar<long>("SELECT revision FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args),
                    session.ExecuteScalar<long>("SELECT confirmed_at FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args),
                    session.ExecuteScalar<byte[]>("SELECT payload FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args),
                    session.ExecuteScalar<byte[]>("SELECT extensions FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args));
                return row.ToState(owner);
            }, cancellationToken);
        }

        public void Upsert(ILocalStorageTransaction transaction, DurableFeatureMutation mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            var session = RequireSession(transaction);
            EnsureOwner(mutation.Owner.Owner);
            if (!transaction.Scope.Equals(scope)) throw new StorageException(StorageFailure.InvalidOwner, "The transaction belongs to another owner.");
            var args = new object[] { mutation.Owner.ViewKey.Value, mutation.FeatureNamespace, mutation.EntityKey };
            if (session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args) == 0)
            {
                session.Execute("INSERT INTO gp_feature_state VALUES (?,?,?,?,?,?,?)", mutation.Owner.ViewKey.Value, mutation.FeatureNamespace, mutation.EntityKey, mutation.Revision, mutation.ConfirmedAtMilliseconds, mutation.CopyPayload(), mutation.CopyExtensions() ?? Array.Empty<byte>());
                return;
            }
            var currentRevision = session.ExecuteScalar<long>("SELECT revision FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args);
            if (mutation.Revision < currentRevision) throw new StorageException(StorageFailure.IdentityConflict, "A feature-state revision would regress.");
            var extensions = mutation.CopyExtensions() ?? session.ExecuteScalar<byte[]>("SELECT extensions FROM gp_feature_state WHERE view_key=? AND feature_namespace=? AND entity_key=?", args);
            session.Execute("UPDATE gp_feature_state SET revision=?,confirmed_at=?,payload=?,extensions=? WHERE view_key=? AND feature_namespace=? AND entity_key=?", mutation.Revision, mutation.ConfirmedAtMilliseconds, mutation.CopyPayload(), extensions, mutation.Owner.ViewKey.Value, mutation.FeatureNamespace, mutation.EntityKey);
        }

        private static SqliteTransactionSession RequireSession(ILocalStorageTransaction transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (!(transaction is SqliteTransactionSession session)) throw new StorageException(StorageFailure.InvalidOwner, "A foreign transaction was supplied.");
            return session;
        }
        private void EnsureOwner(OwnerScope owner)
        {
            if (!string.Equals(owner.Backend.Value, scope.BackendNamespace, StringComparison.Ordinal) ||
                owner.AppId.Value != Guid.Parse(scope.AppId.Value) || owner.UserId.Value != Guid.Parse(scope.AccountId.Value))
                throw new StorageException(StorageFailure.InvalidOwner, "The feature-state owner does not match the database.");
        }

        private static void ValidateIdentity(ScopedOwnerContext owner, string featureNamespace, string entityKey)
        {
            if (!owner.IsValid) throw new ArgumentException("A valid owner is required.", nameof(owner));
            ValidateText(featureNamespace, 128, nameof(featureNamespace));
            ValidateText(entityKey, 256, nameof(entityKey));
        }

        private static void ValidateText(string value, int maximum, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    internal sealed class SqliteFeatureStateRow
    {
        private readonly byte[] payload;
        private readonly byte[] extensions;
        public SqliteFeatureStateRow(string viewKey, string featureNamespace, string entityKey, long revision, long confirmedAt, byte[] payload, byte[] extensions)
        {
            ViewKey = viewKey; FeatureNamespace = featureNamespace; EntityKey = entityKey; Revision = revision; ConfirmedAt = confirmedAt;
            this.payload = payload ?? throw new StorageException(StorageFailure.Corrupt, "A feature-state payload is missing.");
            this.extensions = extensions ?? throw new StorageException(StorageFailure.Corrupt, "Feature-state extensions are missing.");
        }
        public string ViewKey { get; }
        public string FeatureNamespace { get; }
        public string EntityKey { get; }
        public long Revision { get; }
        public long ConfirmedAt { get; }
        public DurableFeatureState ToState(ScopedOwnerContext owner)
        {
            if (!string.Equals(ViewKey, owner.ViewKey.Value, StringComparison.Ordinal)) throw new StorageException(StorageFailure.Corrupt, "A feature-state row escaped its view.");
            return new DurableFeatureState(owner, FeatureNamespace, EntityKey, Revision, ConfirmedAt, payload, extensions);
        }
    }
}
