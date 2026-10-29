using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;
using GamePlatform.Storage.Sqlite.Executor;

namespace GamePlatform.Storage.Sqlite.Outbox
{
    public sealed class SqliteStorePurchaseRecordReader : IStorePurchaseRecordReader
    {
        private readonly SqliteDatabase database;
        private readonly StorageScope scope;
        private readonly IDurableFeatureStateStore state;
        public SqliteStorePurchaseRecordReader(SqliteDatabase database, StorageScope scope, IDurableFeatureStateStore state)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            if (!database.Scope.Equals(scope))
                throw new StorageException(StorageFailure.InvalidOwner, "Invalid database scope.");
            this.scope = scope;
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }
        public Task<IReadOnlyList<StorePurchaseRecord>> ReadAsync(ScopedOwnerContext owner, CancellationToken cancellationToken) =>
            database.ExecuteAsync(scope, transaction =>
            {
                if (!owner.IsValid || owner.Owner.Backend.Value != scope.BackendNamespace ||
                    owner.Owner.AppId.ToString() != scope.AppId.Value || owner.Owner.UserId.ToString() != scope.AccountId.Value)
                    throw new StorageException(StorageFailure.InvalidOwner, "The requested owner does not match the purchase database.");
                var session = (SqliteTransactionSession)transaction;
                var rows = session.Query<Row>("SELECT operation_id AS OperationId, sequence AS Sequence, delivery_state AS DeliveryState, semantic_body AS Body, terminal_result AS Terminal FROM gp_outbox WHERE backend_namespace=? AND app_id=? AND account_id=? AND operation_kind='store.offer.purchase' ORDER BY sequence LIMIT 4097",
                    scope.BackendNamespace, scope.AppId.Value, scope.AccountId.Value);
                if (rows.Count > 4096)
                    throw new StorageException(StorageFailure.Constraint, "Too many retained Store purchases.");
                bool ready = session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_sync_state s JOIN gp_stream_state t ON s.client_stream_id=t.client_stream_id WHERE s.singleton=1 AND s.ready=1 AND t.singleton=1 AND t.ready=1") == 1 &&
                    session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_bootstrap_state WHERE singleton=1") == 0;
                long through = ready ? session.ExecuteScalar<long>("SELECT committed_through FROM gp_sync_state WHERE singleton=1") : 0;
                long visibility = ready ? session.ExecuteScalar<long>("SELECT visibility_generation FROM gp_sync_state WHERE singleton=1") : 0;
                string epoch = ready ? session.ExecuteScalar<string>("SELECT log_epoch FROM gp_sync_state WHERE singleton=1") : "";
                var expected = visibility.ToString(CultureInfo.InvariantCulture) + "|" + epoch;
                var result = new List<StorePurchaseRecord>(rows.Count);
                foreach (var row in rows)
                {
                    var operation = new OperationId(Guid.Parse(row.OperationId));
                    if (session.ExecuteScalar<int>("SELECT COUNT(*) FROM gp_feature_state WHERE view_key=? AND feature_namespace='store' AND payload=?", owner.ViewKey.Value, operation.Value.ToByteArray()) != 1)
                        continue;
                    var binding = state.Read(transaction, owner, "store.purchase.binding", row.OperationId);
                    bool compatible = ready && binding != null && Encoding.UTF8.GetString(binding.CopyPayload()) == expected;
                    result.Add(new StorePurchaseRecord(operation, row.Sequence, row.DeliveryState, row.Body, row.Terminal, compatible, through));
                }
                return (IReadOnlyList<StorePurchaseRecord>)result.AsReadOnly();
            }, cancellationToken);

        private sealed class Row
        {
            public string OperationId { get; set; } = "";
            public long Sequence { get; set; }
            public string DeliveryState { get; set; } = "";
            public byte[] Body { get; set; } = Array.Empty<byte>();
            public byte[]? Terminal { get; set; }
        }
    }
}
