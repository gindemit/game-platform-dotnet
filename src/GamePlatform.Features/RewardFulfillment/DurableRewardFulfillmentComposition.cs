using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.RewardFulfillment;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;

namespace GamePlatform.Features.RewardFulfillment
{
    /// <summary>Production durable CL-107 composition over the caller-owned serialized store and transaction executor.</summary>
    public static class DurableRewardFulfillmentComposition
    {
        public static RewardFulfillmentService Create(ScopedOwnerContext owner, StorageScope scope, IDurableFeatureStateStore state, ISerializedStorageExecutor transactions, Func<long> nowMilliseconds)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (transactions == null)
                throw new ArgumentNullException(nameof(transactions));
            if (nowMilliseconds == null)
                throw new ArgumentNullException(nameof(nowMilliseconds));
            var codec = new DurableRewardFulfillmentStateCodec();
            return new RewardFulfillmentService(owner, scope, state, transactions, codec, new DurableClaimStore(owner, scope, state, transactions, nowMilliseconds), new DurableEvidenceStore(state), nowMilliseconds);
        }
        private sealed class DurableClaimStore : IRewardPresentationClaimStore
        {
            private readonly ScopedOwnerContext captured; private readonly StorageScope scope; private readonly IDurableFeatureStateStore state; private readonly ISerializedStorageExecutor transactions; private readonly Func<long> now;
            public DurableClaimStore(ScopedOwnerContext captured, StorageScope scope, IDurableFeatureStateStore state, ISerializedStorageExecutor transactions, Func<long> now)
            {
                this.captured = captured;
                this.scope = scope;
                this.state = state;
                this.transactions = transactions;
                this.now = now;
            }
            public Task<bool> TryClaimAsync(ScopedOwnerContext owner, Guid grantId, Action<ILocalStorageTransaction> onClaim, CancellationToken token)
            {
                if (owner != captured)
                    throw new RewardFulfillmentOwnerMismatchException();
                if (grantId == Guid.Empty)
                    throw new ArgumentException("A grant identity is required.", nameof(grantId));
                if (onClaim == null)
                    throw new ArgumentNullException(nameof(onClaim));
                return transactions.ExecuteAsync(scope, transaction => { var key = "presentation/" + grantId.ToString("N"); if (state.Read(transaction, owner, "reward-fulfillment", key) != null) return false; state.Upsert(transaction, new DurableFeatureMutation(owner, "reward-fulfillment", key, 1, now(), new byte[] { 1 }, Array.Empty<byte>())); onClaim(transaction); return true; }, token);
            }
        }
        private sealed class DurableEvidenceStore : IRewardProjectionEvidenceStore
        {
            private readonly IDurableFeatureStateStore state; public DurableEvidenceStore(IDurableFeatureStateStore state)
            {
                this.state = state;
            }
            public void InsertOrVerify(ILocalStorageTransaction transaction, ScopedOwnerContext owner, Guid grantId, long feedRevision, long recordedAtMilliseconds, byte[] payload)
            {
                if (payload == null || payload.Length == 0)
                    throw new ArgumentException("Canonical evidence is required.", nameof(payload));
                var key = "evidence/" + grantId.ToString("N");
                var existing = state.Read(transaction, owner, "reward-fulfillment", key);
                if (existing == null)
                {
                    state.Upsert(transaction, new DurableFeatureMutation(owner, "reward-fulfillment", key, feedRevision, recordedAtMilliseconds, payload, Array.Empty<byte>()));
                    return;
                }
                if (existing.Revision != feedRevision || !Same(existing.CopyPayload(), payload))
                    throw new RewardFulfillmentConflictException("A grant cannot replace immutable projection evidence.");
            }
            private static bool Same(byte[] left, byte[] right)
            {
                if (left.Length != right.Length)
                    return false;
                var difference = 0;
                for (var i = 0; i < left.Length; i++)
                    difference |= left[i] ^ right[i];
                return difference == 0;
            }
        }
    }
}
