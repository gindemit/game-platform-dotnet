using System;
using System.Globalization;
using System.Text;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Executor;

namespace GamePlatform.Storage.Sqlite.Outbox
{
    /// <summary>Captures the current installed sync generation and epoch atomically with a purchase command.</summary>
    public sealed class SqliteStorePurchaseAdmissionBinding : IStorePurchaseAdmissionBinding
    {
        private readonly IDurableFeatureStateStore state;
        public SqliteStorePurchaseAdmissionBinding(IDurableFeatureStateStore state) =>
            this.state = state ?? throw new ArgumentNullException(nameof(state));

        public void Stage(ILocalStorageTransaction transaction, ScopedOwnerContext owner, OperationId operationId, long localRevision)
        {
            if (!(transaction is SqliteTransactionSession session))
                throw new ArgumentException("A SQLite transaction is required.", nameof(transaction));
            if (session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_sync_state s JOIN gp_stream_state t ON s.client_stream_id=t.client_stream_id WHERE s.singleton=1 AND s.ready=1 AND t.singleton=1 AND t.ready=1") != 1 ||
                session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_bootstrap_state WHERE singleton=1") != 0)
                throw new StorageException(StorageFailure.NotReady, "A stable installed sync state is required.");
            var visibility = session.ExecuteScalar<long>("SELECT visibility_generation FROM gp_sync_state WHERE singleton=1");
            var epoch = Guid.Parse(session.ExecuteScalar<string>("SELECT log_epoch FROM gp_sync_state WHERE singleton=1"));
            var key = operationId.Value.ToString("D");
            var payload = Encoding.UTF8.GetBytes(visibility.ToString(CultureInfo.InvariantCulture) + "|" + epoch.ToString("D"));
            state.Upsert(transaction, new DurableFeatureMutation(owner, "store.purchase.binding", key, localRevision, 0, payload, Array.Empty<byte>()));
        }
    }
}
