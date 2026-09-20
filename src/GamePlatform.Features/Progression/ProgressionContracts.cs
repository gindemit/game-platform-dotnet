#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using GamePlatform.Core;
using GamePlatform.Features.Contracts;

namespace GamePlatform.Features.Progression
{
    /// <summary>The only currently approved admission label. It explicitly does not claim server gameplay validation.</summary>
    public enum ProgressionOutcomeAuthority { ClientTrustedUnvalidated = 0 }
    public enum ProgressionPendingStatus { AwaitingReceipt = 0, AcceptedAwaitingPull = 1 }

    /// <summary>Game-owned completion data presented to the portable command boundary. This type allocates no progress or reward.</summary>
    public sealed class ProgressionCompletionRequest
    {
        public ProgressionCompletionRequest(ScopedOwnerContext owner, OperationId operationId, ClientStreamId streamId,
            string businessSource, GameplayOutcome outcome, int contentVersion, ProgressionOutcomeAuthority outcomeAuthority)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("A valid operation ID is required.", nameof(operationId));
            if (!streamId.IsValid) throw new ArgumentException("A valid stream ID is required.", nameof(streamId));
            ProgressionValidation.BusinessSource(businessSource, nameof(businessSource));
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            if (contentVersion < 1) throw new ArgumentOutOfRangeException(nameof(contentVersion));
            if (!Enum.IsDefined(typeof(ProgressionOutcomeAuthority), outcomeAuthority)) throw new ArgumentOutOfRangeException(nameof(outcomeAuthority));
            Owner = owner; OperationId = operationId; StreamId = streamId; BusinessSource = businessSource;
            Outcome = outcome; ContentVersion = contentVersion; OutcomeAuthority = outcomeAuthority;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public GameplayOutcome Outcome { get; }
        public int ContentVersion { get; }
        public ProgressionOutcomeAuthority OutcomeAuthority { get; }
    }

    /// <summary>Durable local intent. Accepted means only that the command receipt was retained; pull remains authoritative.</summary>
    public sealed class PendingProgressionCompletion
    {
        public PendingProgressionCompletion(OperationId operationId, ClientStreamId streamId, string businessSource,
            GameplayOutcome outcome, int contentVersion, ProgressionOutcomeAuthority outcomeAuthority, long localRevision,
            ProgressionPendingStatus status)
        {
            if (!operationId.IsValid || !streamId.IsValid) throw new ArgumentException("Valid command identities are required.");
            ProgressionValidation.BusinessSource(businessSource, nameof(businessSource));
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            if (contentVersion < 1 || localRevision < 1) throw new ArgumentOutOfRangeException(contentVersion < 1 ? nameof(contentVersion) : nameof(localRevision));
            if (!Enum.IsDefined(typeof(ProgressionOutcomeAuthority), outcomeAuthority) || !Enum.IsDefined(typeof(ProgressionPendingStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            OperationId = operationId; StreamId = streamId; BusinessSource = businessSource; Outcome = outcome;
            ContentVersion = contentVersion; OutcomeAuthority = outcomeAuthority; LocalRevision = localRevision; Status = status;
        }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public GameplayOutcome Outcome { get; }
        public int ContentVersion { get; }
        public ProgressionOutcomeAuthority OutcomeAuthority { get; }
        public long LocalRevision { get; }
        public ProgressionPendingStatus Status { get; }
    }

    /// <summary>One server-owned semantic progression key/value. It is not a game checkpoint and may not be inferred from completion.</summary>
    public sealed class ProgressionConfirmedState
    {
        public ProgressionConfirmedState(SemanticId stateKey, long value)
        {
            if (!stateKey.IsValid) throw new ArgumentException("A state key is required.", nameof(stateKey));
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            StateKey = stateKey; Value = value;
        }
        public SemanticId StateKey { get; }
        public long Value { get; }
    }

    /// <summary>A complete authoritative projection boundary supplied by bootstrap/pull. Its revision is owned by the synchronization adapter.</summary>
    public sealed class ProgressionConfirmedProjection
    {
        private readonly ReadOnlyCollection<ProgressionConfirmedState> states;
        public ProgressionConfirmedProjection(long revision, long confirmedAtMilliseconds, IReadOnlyList<ProgressionConfirmedState> states)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            ProgressionValidation.Timestamp(confirmedAtMilliseconds, nameof(confirmedAtMilliseconds));
            if (states == null || states.Count > 1024 || states.Any(value => value == null)) throw new ArgumentOutOfRangeException(nameof(states));
            if (states.GroupBy(value => value.StateKey).Any(group => group.Count() != 1)) throw new ArgumentException("Confirmed progression state keys must be unique.", nameof(states));
            Revision = revision; ConfirmedAtMilliseconds = confirmedAtMilliseconds;
            this.states = new ReadOnlyCollection<ProgressionConfirmedState>(states.ToArray());
        }
        public long Revision { get; }
        public long ConfirmedAtMilliseconds { get; }
        public IReadOnlyList<ProgressionConfirmedState> States => states;
    }

    /// <summary>Pull-derived acknowledgement. Only explicitly listed, already accepted operations may leave the pending projection.</summary>
    public sealed class ProgressionConfirmation
    {
        private readonly ReadOnlyCollection<OperationId> confirmedOperationIds;
        public ProgressionConfirmation(ScopedOwnerContext owner, ProgressionConfirmedProjection projection, IReadOnlyList<OperationId> confirmedOperationIds)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            Projection = projection ?? throw new ArgumentNullException(nameof(projection));
            if (confirmedOperationIds == null || confirmedOperationIds.Any(value => !value.IsValid) || confirmedOperationIds.Distinct().Count() != confirmedOperationIds.Count)
                throw new ArgumentException("Confirmed operation identities must be valid and unique.", nameof(confirmedOperationIds));
            Owner = owner; this.confirmedOperationIds = new ReadOnlyCollection<OperationId>(confirmedOperationIds.ToArray());
        }
        public ScopedOwnerContext Owner { get; }
        public ProgressionConfirmedProjection Projection { get; }
        public IReadOnlyList<OperationId> ConfirmedOperationIds => confirmedOperationIds;
    }

    public sealed class ProgressionSnapshot
    {
        private readonly ReadOnlyCollection<PendingProgressionCompletion> pending;
        public ProgressionSnapshot(ProgressionConfirmedProjection? confirmed, IReadOnlyList<PendingProgressionCompletion> pending)
        {
            if (pending == null || pending.Any(value => value == null)) throw new ArgumentNullException(nameof(pending));
            Confirmed = confirmed; this.pending = new ReadOnlyCollection<PendingProgressionCompletion>(pending.ToArray());
        }
        public ProgressionConfirmedProjection? Confirmed { get; }
        public IReadOnlyList<PendingProgressionCompletion> PendingCompletions => pending;
    }

    public sealed class ProgressionAcceptance
    {
        public ProgressionAcceptance(ScopedOwnerContext owner, OperationId operationId)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("A valid operation ID is required.", nameof(operationId));
            Owner = owner; OperationId = operationId;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
    }

    /// <summary>Serialization/transport boundary. An adapter maps the frozen gameplay command and projection; it must not invent rewards.</summary>
    public interface IProgressionStateCodec
    {
        byte[] EncodeCompletionCommand(PendingProgressionCompletion completion);
        byte[] EncodePending(IReadOnlyList<PendingProgressionCompletion> pending);
        IReadOnlyList<PendingProgressionCompletion> DecodePending(long localRevision, ReadOnlySpan<byte> payload);
        byte[] EncodeConfirmed(ProgressionConfirmedProjection projection);
        ProgressionConfirmedProjection DecodeConfirmed(long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload);
    }

    public sealed class ProgressionConflictException : InvalidOperationException { public ProgressionConflictException(string message) : base(message) { } }
    public sealed class ProgressionOwnerMismatchException : InvalidOperationException { public ProgressionOwnerMismatchException() : base("The progression request does not belong to the captured account/view generation.") { } }

    internal static class ProgressionValidation
    {
        private static readonly Regex Source = new Regex("^[^\\r\\n\\0]{1,256}$", RegexOptions.CultureInvariant);
        internal static void BusinessSource(string value, string parameter) { if (string.IsNullOrWhiteSpace(value) || !Source.IsMatch(value)) throw new ArgumentOutOfRangeException(parameter); }
        internal static void Timestamp(long value, string parameter) { if (value < 0 || value > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(parameter); }
    }
}
