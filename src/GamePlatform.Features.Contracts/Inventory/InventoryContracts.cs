#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Inventory
{
    /// <summary>Server-confirmed inventory forms. Stack quantities and instances never share an identity.</summary>
    public enum InventoryEntryKind { Stack = 0, Instance = 1 }
    public enum InventoryInstanceState { Active = 0, Tombstoned = 1 }
    public enum InventoryIntentKind { Use = 0, Consume = 1 }
    public enum InventoryIntentStatus { AwaitingReceipt = 0, AcceptedAwaitingPull = 1 }

    public sealed class InventoryStack
    {
        public InventoryStack(PlatformId definitionId, long quantity, long revision)
        {
            if (!definitionId.IsValid) throw new ArgumentException("A definition ID is required.", nameof(definitionId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            DefinitionId = definitionId; Quantity = quantity; Revision = revision;
        }
        public PlatformId DefinitionId { get; }
        public long Quantity { get; }
        public long Revision { get; }
    }

    public sealed class InventoryInstance
    {
        private const int MaximumPayloadBytes = 16 * 1024;
        private const int MaximumAuditBytes = 16 * 1024;
        private readonly byte[] payload;
        private readonly byte[] auditExtensions;

        public InventoryInstance(PlatformId instanceId, PlatformId definitionId, long revision,
            InventoryInstanceState state, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> auditExtensions)
        {
            if (!instanceId.IsValid || !definitionId.IsValid) throw new ArgumentException("Instance and definition IDs are required.");
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!Enum.IsDefined(typeof(InventoryInstanceState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            if (payload.Length > MaximumPayloadBytes || auditExtensions.Length > MaximumAuditBytes) throw new ArgumentOutOfRangeException(nameof(payload));
            if (state == InventoryInstanceState.Tombstoned && payload.Length != 0) throw new ArgumentException("A tombstone carries no instance payload.", nameof(payload));
            InstanceId = instanceId; DefinitionId = definitionId; Revision = revision; State = state;
            this.payload = payload.ToArray(); this.auditExtensions = auditExtensions.ToArray();
        }
        public PlatformId InstanceId { get; }
        public PlatformId DefinitionId { get; }
        public long Revision { get; }
        public InventoryInstanceState State { get; }
        public byte[] CopyPayload() => (byte[])payload.Clone();
        public byte[] CopyAuditExtensions() => (byte[])auditExtensions.Clone();
    }

    /// <summary>An immutable confirmed projection. Pending intents are deliberately not represented as holdings.</summary>
    public sealed class InventoryConfirmedSnapshot
    {
        private readonly InventoryStack[] stacks;
        private readonly InventoryInstance[] instances;
        public InventoryConfirmedSnapshot(long revision, long confirmedAtMilliseconds, IEnumerable<InventoryStack> stacks, IEnumerable<InventoryInstance> instances)
        {
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (confirmedAtMilliseconds < 0 || confirmedAtMilliseconds > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(nameof(confirmedAtMilliseconds));
            this.stacks = Copy(stacks); this.instances = Copy(instances);
            if (this.stacks.Length > 512 || this.instances.Length > 512) throw new ArgumentOutOfRangeException(nameof(stacks));
            if (this.stacks.Select(value => value.DefinitionId).Distinct().Count() != this.stacks.Length ||
                this.instances.Select(value => value.InstanceId).Distinct().Count() != this.instances.Length)
                throw new ArgumentException("Inventory projection contains duplicate identities.");
            Revision = revision; ConfirmedAtMilliseconds = confirmedAtMilliseconds;
        }
        public long Revision { get; }
        public long ConfirmedAtMilliseconds { get; }
        public IReadOnlyList<InventoryStack> Stacks => Array.AsReadOnly(stacks);
        public IReadOnlyList<InventoryInstance> Instances => Array.AsReadOnly(instances);
        private static T[] Copy<T>(IEnumerable<T> values) where T : class
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var copy = values.ToArray(); if (copy.Any(value => value == null)) throw new ArgumentException("A projection entry is missing.", nameof(values)); return copy;
        }
    }

    public sealed class InventoryIntent
    {
        public InventoryIntent(OperationId operationId, ClientStreamId streamId, string businessSource, InventoryIntentKind kind,
            InventoryEntryKind targetKind, PlatformId targetId, long expectedInventoryRevision, long localRevision)
            : this(operationId, streamId, businessSource, kind, targetKind, targetId, expectedInventoryRevision, localRevision,
                InventoryIntentStatus.AwaitingReceipt, null)
        {
        }

        public InventoryIntent(OperationId operationId, ClientStreamId streamId, string businessSource, InventoryIntentKind kind,
            InventoryEntryKind targetKind, PlatformId targetId, long expectedInventoryRevision, long localRevision,
            InventoryIntentStatus status, long? acceptedInventoryRevision)
        {
            if (!operationId.IsValid || !streamId.IsValid || !targetId.IsValid) throw new ArgumentException("Valid command identities are required.");
            if (!Enum.IsDefined(typeof(InventoryIntentKind), kind) || !Enum.IsDefined(typeof(InventoryEntryKind), targetKind)) throw new ArgumentOutOfRangeException(nameof(kind));
            InventoryValidation.BusinessSource(businessSource, nameof(businessSource));
            if (expectedInventoryRevision <= 0 || localRevision <= 0) throw new ArgumentOutOfRangeException(nameof(expectedInventoryRevision));
            if (!Enum.IsDefined(typeof(InventoryIntentStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (status == InventoryIntentStatus.AcceptedAwaitingPull && (!acceptedInventoryRevision.HasValue || acceptedInventoryRevision.Value <= expectedInventoryRevision))
                throw new ArgumentException("An accepted inventory intent requires a newer resulting revision.", nameof(acceptedInventoryRevision));
            if (status != InventoryIntentStatus.AcceptedAwaitingPull && acceptedInventoryRevision.HasValue)
                throw new ArgumentException("Only accepted inventory intents carry a resulting revision.", nameof(acceptedInventoryRevision));
            OperationId = operationId; StreamId = streamId; BusinessSource = businessSource; Kind = kind; TargetKind = targetKind;
            TargetId = targetId; ExpectedInventoryRevision = expectedInventoryRevision; LocalRevision = localRevision;
            Status = status; AcceptedInventoryRevision = acceptedInventoryRevision;
        }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public InventoryIntentKind Kind { get; }
        public InventoryEntryKind TargetKind { get; }
        public PlatformId TargetId { get; }
        public long ExpectedInventoryRevision { get; }
        public long LocalRevision { get; }
        public InventoryIntentStatus Status { get; }
        public long? AcceptedInventoryRevision { get; }
    }

    /// <summary>Receipt outcome only. It has no holdings, quantities, or grant instructions.</summary>
    public sealed class InventoryIntentAcceptance
    {
        public InventoryIntentAcceptance(ScopedOwnerContext owner, OperationId operationId, long resultingInventoryRevision)
        {
            if (!owner.IsValid || !operationId.IsValid) throw new ArgumentException("A captured owner and operation ID are required.");
            if (resultingInventoryRevision <= 0) throw new ArgumentOutOfRangeException(nameof(resultingInventoryRevision));
            Owner = owner; OperationId = operationId; ResultingInventoryRevision = resultingInventoryRevision;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public long ResultingInventoryRevision { get; }
    }

    public sealed class InventorySnapshot
    {
        private readonly InventoryIntent[] pending;
        public InventorySnapshot(InventoryConfirmedSnapshot? confirmed, IEnumerable<InventoryIntent> pending)
        {
            Confirmed = confirmed;
            if (pending == null) throw new ArgumentNullException(nameof(pending));
            this.pending = pending.ToArray();
            if (this.pending.Length > 64 || this.pending.Any(value => value == null) || this.pending.Select(value => value.OperationId).Distinct().Count() != this.pending.Length)
                throw new ArgumentException("Pending inventory intents are invalid.", nameof(pending));
        }
        public InventoryConfirmedSnapshot? Confirmed { get; }
        public IReadOnlyList<InventoryIntent> PendingIntents => Array.AsReadOnly(pending);
    }

    public sealed class InventoryIntentRequest
    {
        public InventoryIntentRequest(ScopedOwnerContext owner, OperationId operationId, ClientStreamId streamId, string businessSource,
            InventoryIntentKind kind, InventoryEntryKind targetKind, PlatformId targetId)
        {
            if (!owner.IsValid || !operationId.IsValid || !streamId.IsValid || !targetId.IsValid) throw new ArgumentException("Valid captured command identity is required.");
            if (!Enum.IsDefined(typeof(InventoryIntentKind), kind) || !Enum.IsDefined(typeof(InventoryEntryKind), targetKind)) throw new ArgumentOutOfRangeException(nameof(kind));
            InventoryValidation.BusinessSource(businessSource, nameof(businessSource));
            Owner = owner; OperationId = operationId; StreamId = streamId; BusinessSource = businessSource; Kind = kind; TargetKind = targetKind; TargetId = targetId;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public InventoryIntentKind Kind { get; }
        public InventoryEntryKind TargetKind { get; }
        public PlatformId TargetId { get; }
    }

    /// <summary>Frozen semantic authority boundary; it does not create a provider route or make an offline holding spendable.</summary>
    public interface IInventoryIntentAuthority
    {
        InventoryIntentAuthorization Authorize(InventoryIntentRequest request);
    }
    public sealed class InventoryIntentAuthorization
    {
        public InventoryIntentAuthorization(bool permitted, string? operationKind, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 128) throw new ArgumentOutOfRangeException(nameof(reason));
            if (permitted && (string.IsNullOrWhiteSpace(operationKind) || operationKind.Length > 128)) throw new ArgumentOutOfRangeException(nameof(operationKind));
            if (!permitted && operationKind != null) throw new ArgumentException("Denied intents have no operation route.", nameof(operationKind));
            Permitted = permitted; OperationKind = operationKind; Reason = reason;
        }
        public bool Permitted { get; }
        public string? OperationKind { get; }
        public string Reason { get; }
    }

    public interface IInventoryStateCodec
    {
        byte[] EncodeConfirmed(InventoryConfirmedSnapshot snapshot);
        InventoryConfirmedSnapshot DecodeConfirmed(long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions);
        byte[] EncodePending(IReadOnlyList<InventoryIntent> intents);
        IReadOnlyList<InventoryIntent> DecodePending(long localRevision, ReadOnlySpan<byte> payload);
        byte[] EncodeIntentCommand(InventoryIntent intent);
        void ValidateInstanceExtensions(ReadOnlySpan<byte> extensions);
    }

    public sealed class InventoryOwnerMismatchException : InvalidOperationException { public InventoryOwnerMismatchException() : base("Inventory data does not belong to the captured account/view generation.") { } }
    public sealed class InventoryIntentRejectedException : InvalidOperationException { public InventoryIntentRejectedException(string reason) : base(reason) { } }
    public sealed class InventoryProjectionConflictException : InvalidOperationException { public InventoryProjectionConflictException(string reason) : base(reason) { } }

    internal static class InventoryValidation
    {
        private static readonly Regex Source = new Regex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,255}$", RegexOptions.CultureInvariant);
        internal static void BusinessSource(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || !Source.IsMatch(value)) throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
