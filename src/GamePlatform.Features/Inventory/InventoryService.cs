#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;
using GamePlatform.Features.Contracts.Catalog;
using GamePlatform.Features.Contracts.Inventory;
using GamePlatform.Features.Contracts.Snapshots;
using GamePlatform.Storage.Abstractions;
using GamePlatform.Storage.Abstractions.FeatureState;
using GamePlatform.Storage.Abstractions.Outbox;

namespace GamePlatform.Features.Inventory
{
    /// <summary>Portable confirmed-holding projection. It never grants, decrements, or treats pending intent as value.</summary>
    public sealed class InventoryService : IDisposable
    {
        private const string Namespace = "inventory";
        private const string ConfirmedKey = "confirmed";
        private const string PendingKey = "pending";
        private readonly ScopedOwnerContext owner;
        private readonly StorageScope scope;
        private readonly IDurableFeatureStateStore state;
        private readonly ISerializedStorageExecutor transactions;
        private readonly IAtomicCommandStore commands;
        private readonly IInventoryStateCodec codec;
        private readonly IInventoryIntentAuthority authority;
        private readonly Func<long> nowMilliseconds;
        private readonly SemaphoreSlim mutation = new SemaphoreSlim(1, 1);
        private bool disposed;

        public InventoryService(ScopedOwnerContext owner, StorageScope scope, IDurableFeatureStateStore state,
            ISerializedStorageExecutor transactions, IAtomicCommandStore commands, IInventoryStateCodec codec,
            IInventoryIntentAuthority authority, Func<long> nowMilliseconds)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!Matches(scope, owner.Owner)) throw new ArgumentException("The inventory scope does not match the captured owner.", nameof(scope));
            this.owner = owner; this.scope = scope; this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions)); this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec)); this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
            this.nowMilliseconds = nowMilliseconds ?? throw new ArgumentNullException(nameof(nowMilliseconds));
        }

        public Task<FeatureSnapshot<InventorySnapshot>> ReadCachedAsync(ScopedOwnerContext requestedOwner, CancellationToken cancellationToken) =>
            ReadSnapshotAsync(EnsureOwner(requestedOwner), SnapshotFreshness.Stale, cancellationToken);

        /// <summary>Accepts only a server-confirmed projection after validating the matching app-visible catalog snapshot.</summary>
        public async Task ApplyConfirmedAsync(ScopedOwnerContext requestedOwner, InventoryConfirmedSnapshot confirmed,
            CatalogSnapshot catalog, CancellationToken cancellationToken)
        {
            var exactOwner = EnsureOwner(requestedOwner);
            if (confirmed == null) throw new ArgumentNullException(nameof(confirmed));
            ValidateCatalog(exactOwner, confirmed, catalog);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                if (current.Confirmed != null && confirmed.Revision < current.Confirmed.Revision) return;
                if (current.Confirmed != null && confirmed.Revision == current.Confirmed.Revision)
                {
                    if (!Equivalent(current.Confirmed, confirmed)) throw new InventoryProjectionConflictException("A confirmed inventory revision changed meaning.");
                    return;
                }
                var retained = current.Pending.Where(value => value.Status != InventoryIntentStatus.AcceptedAwaitingPull ||
                    confirmed.Revision < value.AcceptedInventoryRevision!.Value).ToArray();
                await transactions.ExecuteAsync(scope, transaction =>
                {
                    ApplyConfirmedProjection(transaction, exactOwner, current.Confirmed?.Revision ?? 0, confirmed, catalog);
                    if (retained.Length != current.Pending.Count)
                        WritePending(transaction, exactOwner, retained, current.PendingRevision);
                    return true;
                }, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
        }

        /// <summary>
        /// Applies a validated confirmed projection inside a caller-owned transaction. The caller must pass its durable
        /// prior projection revision from the same private-feed transaction; this method never advances a cursor itself.
        /// </summary>
        public void ApplyConfirmedProjection(ILocalStorageTransaction transaction, ScopedOwnerContext requestedOwner,
            long durablePriorRevision, InventoryConfirmedSnapshot confirmed, CatalogSnapshot catalog)
        {
            var exactOwner = EnsureOwner(requestedOwner);
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (!transaction.Scope.Equals(scope)) throw new StorageException(StorageFailure.InvalidOwner, "The inventory transaction belongs to another scope.");
            if (durablePriorRevision < 0 || confirmed == null) throw new ArgumentOutOfRangeException(nameof(durablePriorRevision));
            if (confirmed.Revision <= durablePriorRevision) throw new InventoryProjectionConflictException("A borrowed inventory projection must advance its durable revision.");
            ValidateCatalog(exactOwner, confirmed, catalog);
            var payload = codec.EncodeConfirmed(confirmed); ValidatePayload(payload, "confirmed inventory");
            state.Upsert(transaction, new DurableFeatureMutation(exactOwner, Namespace, ConfirmedKey, confirmed.Revision,
                confirmed.ConfirmedAtMilliseconds, payload, Array.Empty<byte>()));
        }

        /// <summary>Records an authorized use/consume intent atomically with the immutable outbox. It does not change holdings.</summary>
        public async Task<FeatureSnapshot<InventorySnapshot>> SubmitIntentAsync(InventoryIntentRequest request,
            CatalogSnapshot catalog, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var exactOwner = EnsureOwner(request.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                if (current.Confirmed == null) throw new InventoryIntentRejectedException("inventory_missing_confirmed_state");
                EnsureTargetIsConfirmed(current.Confirmed, request);
                ValidateCatalog(exactOwner, current.Confirmed, catalog);
                if (!AllowsUse(catalog, request, current.Confirmed)) throw new InventoryIntentRejectedException("inventory_target_not_usable");
                var existing = current.Pending.FirstOrDefault(value => value.OperationId == request.OperationId);
                if (existing != null)
                {
                    if (Equivalent(existing, request)) return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
                    throw new InventoryIntentRejectedException("inventory_operation_identity_conflict");
                }
                var authorization = authority.Authorize(request);
                if (authorization == null || !authorization.Permitted) throw new InventoryIntentRejectedException(authorization == null ? "inventory_authority_unavailable" : authorization.Reason);
                var draftIntent = new InventoryIntent(request.OperationId, request.StreamId, request.BusinessSource, request.Kind,
                    request.TargetKind, request.TargetId, current.Confirmed.Revision, 1);
                var commandBody = codec.EncodeIntentCommand(draftIntent); ValidatePayload(commandBody, "inventory intent command");
                var draft = new CommandDraft(exactOwner.Owner, request.OperationId, request.StreamId, authorization.OperationKind!, 1, 1, commandBody);
                await commands.CommitAsync(request.BusinessSource, draft, (transaction, localRevision) =>
                {
                    var intent = new InventoryIntent(request.OperationId, request.StreamId, request.BusinessSource, request.Kind,
                        request.TargetKind, request.TargetId, current.Confirmed.Revision, localRevision);
                    var pending = current.Pending.Concat(new[] { intent }).ToArray();
                    var payload = codec.EncodePending(pending); ValidatePayload(payload, "pending inventory");
                    state.Upsert(transaction, new DurableFeatureMutation(exactOwner, Namespace, PendingKey, localRevision, Now(), payload, Array.Empty<byte>()));
                }, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Receipts may discard only the matching local intent; a receipt never increments or decrements a confirmed holding.</summary>
        public async Task<FeatureSnapshot<InventorySnapshot>> RejectIntentAsync(ScopedOwnerContext requestedOwner, OperationId operationId, CancellationToken cancellationToken)
        {
            var exactOwner = EnsureOwner(requestedOwner);
            if (!operationId.IsValid) throw new ArgumentException("A valid operation ID is required.", nameof(operationId));
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                var retained = current.Pending.Where(value => value.OperationId != operationId).ToArray();
                if (retained.Length != current.Pending.Count)
                    await WritePendingAsync(exactOwner, retained, current.PendingRevision, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Stores a receipt-derived accepted-awaiting-pull marker without changing confirmed holdings.</summary>
        public async Task<FeatureSnapshot<InventorySnapshot>> MarkAcceptedAwaitingPullAsync(InventoryIntentAcceptance acceptance, CancellationToken cancellationToken)
        {
            if (acceptance == null) throw new ArgumentNullException(nameof(acceptance));
            var exactOwner = EnsureOwner(acceptance.Owner);
            await mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var current = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
                var found = current.Pending.FirstOrDefault(value => value.OperationId == acceptance.OperationId);
                if (found == null) throw new InventoryIntentRejectedException("inventory_pending_intent_missing");
                if (acceptance.ResultingInventoryRevision <= found.ExpectedInventoryRevision)
                    throw new InventoryProjectionConflictException("The accepted inventory receipt did not advance the expected revision.");
                if (current.Confirmed != null && current.Confirmed.Revision >= acceptance.ResultingInventoryRevision)
                {
                    var retained = current.Pending.Where(value => value.OperationId != acceptance.OperationId).ToArray();
                    await WritePendingAsync(exactOwner, retained, current.PendingRevision, cancellationToken).ConfigureAwait(false);
                    return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
                }
                if (found.Status == InventoryIntentStatus.AcceptedAwaitingPull)
                {
                    if (found.AcceptedInventoryRevision != acceptance.ResultingInventoryRevision)
                        throw new InventoryProjectionConflictException("The accepted inventory receipt conflicts with the retained result.");
                    return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
                }
                var replaced = current.Pending.Select(value => value.OperationId != acceptance.OperationId ? value :
                    new InventoryIntent(value.OperationId, value.StreamId, value.BusinessSource, value.Kind, value.TargetKind, value.TargetId,
                        value.ExpectedInventoryRevision, value.LocalRevision, InventoryIntentStatus.AcceptedAwaitingPull, acceptance.ResultingInventoryRevision)).ToArray();
                await WritePendingAsync(exactOwner, replaced, current.PendingRevision, cancellationToken).ConfigureAwait(false);
            }
            finally { mutation.Release(); }
            return await ReadSnapshotAsync(exactOwner, SnapshotFreshness.Stale, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose() { if (disposed) return; disposed = true; mutation.Dispose(); }

        private async Task<FeatureSnapshot<InventorySnapshot>> ReadSnapshotAsync(ScopedOwnerContext exactOwner, SnapshotFreshness freshness, CancellationToken cancellationToken)
        {
            var durable = await ReadDurableAsync(exactOwner, cancellationToken).ConfigureAwait(false);
            if (durable.Confirmed == null && durable.Pending.Count == 0)
                return new FeatureSnapshot<InventorySnapshot>(exactOwner, 0, FeatureSnapshotState.Missing, SnapshotFreshness.Missing, null, null, null);
            var snapshot = new InventorySnapshot(durable.Confirmed, durable.Pending);
            var revision = durable.Confirmed?.Revision ?? 0;
            if (durable.Pending.Count != 0)
                return new FeatureSnapshot<InventorySnapshot>(exactOwner, revision, FeatureSnapshotState.Pending, freshness, snapshot, durable.Confirmed?.ConfirmedAtMilliseconds, null);
            return new FeatureSnapshot<InventorySnapshot>(exactOwner, revision, freshness == SnapshotFreshness.Current ? FeatureSnapshotState.Available : FeatureSnapshotState.Stale,
                freshness, snapshot, durable.Confirmed!.ConfirmedAtMilliseconds, null);
        }

        private async Task<(InventoryConfirmedSnapshot? Confirmed, IReadOnlyList<InventoryIntent> Pending, long PendingRevision)> ReadDurableAsync(ScopedOwnerContext exactOwner, CancellationToken cancellationToken)
        {
            var confirmed = await state.ReadAsync(exactOwner, Namespace, ConfirmedKey, cancellationToken).ConfigureAwait(false);
            var pending = await state.ReadAsync(exactOwner, Namespace, PendingKey, cancellationToken).ConfigureAwait(false);
            InventoryConfirmedSnapshot? decoded = null;
            if (confirmed != null)
            {
                codec.ValidateInstanceExtensions(confirmed.CopyExtensions());
                decoded = codec.DecodeConfirmed(confirmed.Revision, confirmed.ConfirmedAtMilliseconds, confirmed.CopyPayload(), confirmed.CopyExtensions());
                if (decoded == null || decoded.Revision != confirmed.Revision) throw new InventoryProjectionConflictException("The inventory codec returned an invalid confirmed state.");
            }
            if (pending == null || pending.PayloadLength == 0) return (decoded, Array.Empty<InventoryIntent>(), pending?.Revision ?? 0);
            var decodedPending = codec.DecodePending(pending.Revision, pending.CopyPayload());
            if (decodedPending == null || decodedPending.Any(value => value == null)) throw new InventoryProjectionConflictException("The inventory codec returned invalid pending state.");
            return (decoded, decodedPending.ToArray(), pending.Revision);
        }

        private Task WritePendingAsync(ScopedOwnerContext exactOwner, IReadOnlyList<InventoryIntent> pending, long currentRevision, CancellationToken cancellationToken) =>
            transactions.ExecuteAsync(scope, transaction =>
            {
                WritePending(transaction, exactOwner, pending, currentRevision);
                return true;
            }, cancellationToken);

        private void WritePending(ILocalStorageTransaction transaction, ScopedOwnerContext exactOwner, IReadOnlyList<InventoryIntent> pending, long currentRevision)
        {
            var revision = Math.Max(currentRevision, pending.Count == 0 ? 1L : pending.Max(value => value.LocalRevision));
            var payload = pending.Count == 0 ? Array.Empty<byte>() : codec.EncodePending(pending);
            if (payload.Length != 0) ValidatePayload(payload, "pending inventory");
            state.Upsert(transaction, new DurableFeatureMutation(exactOwner, Namespace, PendingKey, revision, Now(), payload, Array.Empty<byte>()));
        }

        private ScopedOwnerContext EnsureOwner(ScopedOwnerContext requested) { if (disposed) throw new ObjectDisposedException(nameof(InventoryService)); if (requested != owner) throw new InventoryOwnerMismatchException(); return requested; }
        private long Now() { var value = nowMilliseconds(); if (value < 0 || value > 253_402_300_799_999L) throw new InvalidOperationException("Inventory clock is out of bounds."); return value; }
        private static bool Matches(StorageScope scope, OwnerScope owner) => string.Equals(scope.BackendNamespace, owner.Backend.Value, StringComparison.Ordinal) && string.Equals(scope.AppId.Value, owner.AppId.ToString(), StringComparison.Ordinal) && string.Equals(scope.AccountId.Value, owner.UserId.ToString(), StringComparison.Ordinal);
        private static void ValidatePayload(byte[]? payload, string label) { if (payload == null || payload.Length == 0 || payload.Length > 262_144) throw new InvalidOperationException("The " + label + " payload is invalid."); }
        private void ValidateCatalog(ScopedOwnerContext exactOwner, InventoryConfirmedSnapshot confirmed, CatalogSnapshot catalog)
        {
            if (catalog == null || catalog.Request.Owner != exactOwner) throw new InventoryOwnerMismatchException();
            foreach (var stack in confirmed.Stacks) EnsureVisibleItem(catalog, stack.DefinitionId);
            foreach (var instance in confirmed.Instances.Where(value => value.State == InventoryInstanceState.Active))
            {
                codec.ValidateInstanceExtensions(instance.CopyAuditExtensions()); EnsureVisibleItem(catalog, instance.DefinitionId);
            }
        }
        private static void EnsureVisibleItem(CatalogSnapshot catalog, PlatformId definitionId)
        {
            var entry = catalog.Entries.SingleOrDefault(value => value.Definition.DefinitionId.Equals(definitionId));
            if (entry == null || entry.Definition.Kind != CatalogResourceKind.Item || !entry.Binding.Permits(CatalogResourceKind.Item, CatalogPermission.Visible))
                throw new InventoryProjectionConflictException("An inventory entry is unavailable in the exact app-visible catalog.");
        }
        private static bool AllowsUse(CatalogSnapshot catalog, InventoryIntentRequest request, InventoryConfirmedSnapshot confirmed)
        {
            var definition = request.TargetKind == InventoryEntryKind.Stack ? request.TargetId : confirmed.Instances.Single(value => value.InstanceId.Equals(request.TargetId)).DefinitionId;
            var entry = catalog.Entries.SingleOrDefault(value => value.Definition.DefinitionId.Equals(definition));
            return entry != null && entry.Binding.Permits(CatalogResourceKind.Item, CatalogPermission.Use);
        }
        private static void EnsureTargetIsConfirmed(InventoryConfirmedSnapshot confirmed, InventoryIntentRequest request)
        {
            var found = request.TargetKind == InventoryEntryKind.Stack
                ? confirmed.Stacks.Any(value => value.DefinitionId.Equals(request.TargetId))
                : confirmed.Instances.Any(value => value.InstanceId.Equals(request.TargetId) && value.State == InventoryInstanceState.Active);
            if (!found) throw new InventoryIntentRejectedException("inventory_target_not_confirmed");
        }
        private static bool Equivalent(InventoryIntent existing, InventoryIntentRequest request) => existing.StreamId == request.StreamId && string.Equals(existing.BusinessSource, request.BusinessSource, StringComparison.Ordinal) && existing.Kind == request.Kind && existing.TargetKind == request.TargetKind && existing.TargetId.Equals(request.TargetId);
        private static bool Equivalent(InventoryConfirmedSnapshot left, InventoryConfirmedSnapshot right)
        {
            if (left.ConfirmedAtMilliseconds != right.ConfirmedAtMilliseconds || left.Stacks.Count != right.Stacks.Count || left.Instances.Count != right.Instances.Count) return false;
            var rightStacks = right.Stacks.ToDictionary(value => value.DefinitionId);
            foreach (var leftStack in left.Stacks)
            {
                if (!rightStacks.TryGetValue(leftStack.DefinitionId, out var rightStack) || leftStack.Quantity != rightStack.Quantity || leftStack.Revision != rightStack.Revision) return false;
            }
            var rightInstances = right.Instances.ToDictionary(value => value.InstanceId);
            foreach (var leftInstance in left.Instances)
            {
                if (!rightInstances.TryGetValue(leftInstance.InstanceId, out var rightInstance)) return false;
                if (!leftInstance.InstanceId.Equals(rightInstance.InstanceId) || !leftInstance.DefinitionId.Equals(rightInstance.DefinitionId) ||
                    leftInstance.Revision != rightInstance.Revision || leftInstance.State != rightInstance.State ||
                    !EqualBytes(leftInstance.CopyPayload(), rightInstance.CopyPayload()) ||
                    !EqualBytes(leftInstance.CopyAuditExtensions(), rightInstance.CopyAuditExtensions())) return false;
            }
            return true;
        }
        private static bool EqualBytes(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }
}
