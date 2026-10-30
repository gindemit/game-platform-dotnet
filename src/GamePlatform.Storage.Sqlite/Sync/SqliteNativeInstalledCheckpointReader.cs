using System;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.Sync;
using GamePlatform.Storage.Sqlite.Executor;
namespace GamePlatform.Storage.Sqlite.Sync
{
    public sealed class SqliteNativeInstalledCheckpointReader : INativeInstalledCheckpointReader
    {
        private readonly SqliteDatabase database;
        private readonly StorageScope scope;
        public SqliteNativeInstalledCheckpointReader(SqliteDatabase database, StorageScope scope)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            if (!database.Scope.Equals(scope))
                throw new StorageException(StorageFailure.InvalidOwner, "Invalid checkpoint scope.");
            this.scope = scope;
        }
        public Task<NativeInstalledCheckpoint?> ReadAsync(ScopedOwnerContext owner, CancellationToken token)
        {
            if (!owner.IsValid || owner.ViewKey.Value != "private" || owner.Owner.Backend.Value != scope.BackendNamespace ||
                owner.Owner.AppId.ToString() != scope.AppId.Value || owner.Owner.UserId.ToString() != scope.AccountId.Value)
                throw new StorageException(StorageFailure.InvalidOwner, "The checkpoint owner changed.");
            return database.ExecuteAsync(scope, transaction =>
            {
                var session = (SqliteTransactionSession)transaction;
                if (session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_sync_state s JOIN gp_stream_state t ON s.client_stream_id=t.client_stream_id WHERE s.singleton=1 AND s.ready=1 AND t.singleton=1 AND t.ready=1") != 1 ||
                    session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_bootstrap_state WHERE singleton=1") != 0)
                    return null;
                return new NativeInstalledCheckpoint(session.ExecuteScalar<long>("SELECT committed_through FROM gp_sync_state WHERE singleton=1"),
                    session.ExecuteScalar<long>("SELECT visibility_generation FROM gp_sync_state WHERE singleton=1"),
                    Guid.Parse(session.ExecuteScalar<string>("SELECT log_epoch FROM gp_sync_state WHERE singleton=1")));
            }, token);
        }
    }
}
