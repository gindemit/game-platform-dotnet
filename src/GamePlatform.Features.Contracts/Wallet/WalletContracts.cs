#nullable enable
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GamePlatform.Core;

namespace GamePlatform.Features.Contracts.Wallet
{
    /// <summary>Stable server-defined currency identity. It carries neither a balance nor authority.</summary>
    public sealed class WalletCurrency
    {
        public WalletCurrency(PlatformId definitionId, SemanticId semanticKey)
        {
            if (!definitionId.IsValid) throw new ArgumentException("A stable currency definition ID is required.", nameof(definitionId));
            if (!semanticKey.IsValid) throw new ArgumentException("A currency semantic key is required.", nameof(semanticKey));
            DefinitionId = definitionId; SemanticKey = semanticKey;
        }
        public PlatformId DefinitionId { get; }
        public SemanticId SemanticKey { get; }
    }

    /// <summary>One server-confirmed exact signed-64 balance. Local code never derives a replacement balance.</summary>
    public sealed class WalletConfirmedBalance
    {
        private readonly byte[] extensions;
        public WalletConfirmedBalance(WalletCurrency currency, long balance, long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> extensions)
        {
            Currency = currency ?? throw new ArgumentNullException(nameof(currency));
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            WalletValidation.Timestamp(confirmedAtMilliseconds, nameof(confirmedAtMilliseconds));
            if (extensions.Length > WalletValidation.MaximumExtensionBytes) throw new ArgumentOutOfRangeException(nameof(extensions));
            Balance = balance; Revision = revision; ConfirmedAtMilliseconds = confirmedAtMilliseconds;
            this.extensions = extensions.ToArray();
        }
        public WalletCurrency Currency { get; }
        public long Balance { get; }
        public long Revision { get; }
        public long ConfirmedAtMilliseconds { get; }
        public byte[] CopyExtensions() => (byte[])extensions.Clone();
    }

    /// <summary>Immutable client request to ask the backend to spend a positive exact amount.</summary>
    public sealed class WalletSpendIntent
    {
        public WalletSpendIntent(WalletCurrency currency, long expectedRevision, long amount)
        {
            Currency = currency ?? throw new ArgumentNullException(nameof(currency));
            if (expectedRevision <= 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            ExpectedRevision = expectedRevision; Amount = amount;
        }
        public WalletCurrency Currency { get; }
        public long ExpectedRevision { get; }
        public long Amount { get; }
    }

    public enum WalletPendingStatus { AwaitingReceipt = 0, AcceptedAwaitingPull = 1, Rejected = 2 }

    /// <summary>Durable intent state; it is intentionally separate from the confirmed balance.</summary>
    public sealed class PendingWalletSpend
    {
        public PendingWalletSpend(OperationId operationId, ClientStreamId streamId, string businessSource, WalletSpendIntent intent,
            long localRevision, WalletPendingStatus status, long? acceptedRevision, string? rejectionCode)
        {
            if (!operationId.IsValid) throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (!streamId.IsValid) throw new ArgumentException("A valid stream identity is required.", nameof(streamId));
            WalletValidation.BusinessSource(businessSource, nameof(businessSource));
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            if (localRevision <= 0) throw new ArgumentOutOfRangeException(nameof(localRevision));
            if (!Enum.IsDefined(typeof(WalletPendingStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (status == WalletPendingStatus.AcceptedAwaitingPull && (!acceptedRevision.HasValue || acceptedRevision.Value <= intent.ExpectedRevision))
                throw new ArgumentException("An accepted pending spend requires a newer confirmed revision.", nameof(acceptedRevision));
            if (status != WalletPendingStatus.AcceptedAwaitingPull && acceptedRevision.HasValue)
                throw new ArgumentException("Only an accepted pending spend carries an accepted revision.", nameof(acceptedRevision));
            if (status == WalletPendingStatus.Rejected) WalletValidation.Diagnostic(rejectionCode, nameof(rejectionCode));
            else if (rejectionCode != null) throw new ArgumentException("Only a rejected pending spend carries a rejection code.", nameof(rejectionCode));
            OperationId = operationId; StreamId = streamId; BusinessSource = businessSource; LocalRevision = localRevision;
            Status = status; AcceptedRevision = acceptedRevision; RejectionCode = rejectionCode;
        }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public WalletSpendIntent Intent { get; }
        public long LocalRevision { get; }
        public WalletPendingStatus Status { get; }
        public long? AcceptedRevision { get; }
        public string? RejectionCode { get; }
    }

    public sealed class WalletSnapshot
    {
        public WalletSnapshot(WalletConfirmedBalance? confirmed, PendingWalletSpend? pending)
        { Confirmed = confirmed; Pending = pending; }
        public WalletConfirmedBalance? Confirmed { get; }
        public PendingWalletSpend? Pending { get; }
    }

    public sealed class WalletBalanceRequest
    {
        public WalletBalanceRequest(ScopedOwnerContext owner, WalletCurrency currency)
        { if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner)); Owner = owner; Currency = currency ?? throw new ArgumentNullException(nameof(currency)); }
        public ScopedOwnerContext Owner { get; }
        public WalletCurrency Currency { get; }
    }

    public sealed class WalletSpendRequest
    {
        public WalletSpendRequest(ScopedOwnerContext owner, OperationId operationId, ClientStreamId streamId, string businessSource, WalletSpendIntent intent)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (!streamId.IsValid) throw new ArgumentException("A valid stream identity is required.", nameof(streamId));
            WalletValidation.BusinessSource(businessSource, nameof(businessSource));
            Owner = owner; OperationId = operationId; StreamId = streamId; BusinessSource = businessSource; Intent = intent ?? throw new ArgumentNullException(nameof(intent));
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string BusinessSource { get; }
        public WalletSpendIntent Intent { get; }
    }

    /// <summary>A receipt only records the expected future server projection revision; it never includes a locally trusted balance.</summary>
    public sealed class WalletSpendAcceptance
    {
        public WalletSpendAcceptance(ScopedOwnerContext owner, OperationId operationId, long resultingRevision)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            if (resultingRevision <= 0) throw new ArgumentOutOfRangeException(nameof(resultingRevision));
            Owner = owner; OperationId = operationId; ResultingRevision = resultingRevision;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public long ResultingRevision { get; }
    }

    public sealed class WalletSpendRejection
    {
        public WalletSpendRejection(ScopedOwnerContext owner, OperationId operationId, string code)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            WalletValidation.Diagnostic(code, nameof(code)); Owner = owner; OperationId = operationId; Code = code;
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public string Code { get; }
    }

    public sealed class WalletRemoteRead
    {
        public WalletRemoteRead(ScopedOwnerContext owner, WalletConfirmedBalance confirmed)
        { if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner)); Owner = owner; Confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed)); }
        public ScopedOwnerContext Owner { get; }
        public WalletConfirmedBalance Confirmed { get; }
    }

    /// <summary>Fresh backend authorization for one immutable intent. This is not a balance, grant, receipt or debit.</summary>
    public sealed class WalletSpendAuthorization
    {
        public WalletSpendAuthorization(ScopedOwnerContext owner, OperationId operationId, string businessSource, WalletSpendIntent intent)
        {
            if (!owner.IsValid) throw new ArgumentException("A captured owner is required.", nameof(owner));
            if (!operationId.IsValid) throw new ArgumentException("A valid operation identity is required.", nameof(operationId));
            WalletValidation.BusinessSource(businessSource, nameof(businessSource));
            Owner = owner; OperationId = operationId; BusinessSource = businessSource; Intent = intent ?? throw new ArgumentNullException(nameof(intent));
        }
        public ScopedOwnerContext Owner { get; }
        public OperationId OperationId { get; }
        public string BusinessSource { get; }
        public WalletSpendIntent Intent { get; }
    }

    /// <summary>Feature-owned remote boundary. Implementations must authenticate and validate funds online before authorizing admission.</summary>
    public interface IWalletRemote
    {
        Task<WalletRemoteRead> ReadAsync(WalletBalanceRequest request, CancellationToken cancellationToken);
        Task<WalletSpendAuthorization> AuthorizeSpendAsync(WalletSpendRequest request, CancellationToken cancellationToken);
    }

    public interface IWalletStateCodec
    {
        byte[] EncodeConfirmed(WalletConfirmedBalance confirmed);
        WalletConfirmedBalance DecodeConfirmed(long revision, long confirmedAtMilliseconds, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> extensions);
        byte[] EncodePending(PendingWalletSpend pending);
        PendingWalletSpend DecodePending(long localRevision, ReadOnlySpan<byte> payload);
        byte[] EncodeSpendCommand(WalletSpendIntent intent);
        void ValidateExtensions(ReadOnlySpan<byte> extensions);
    }

    public sealed class WalletConflictException : InvalidOperationException { public WalletConflictException(string message) : base(message) { } }
    public sealed class WalletOwnerMismatchException : InvalidOperationException { public WalletOwnerMismatchException() : base("The wallet result does not belong to the captured account/view generation.") { } }
    public sealed class WalletOfflineSpendException : InvalidOperationException { public WalletOfflineSpendException() : base("A cached wallet balance cannot authorize a spend.") { } }

    internal static class WalletValidation
    {
        internal const int MaximumExtensionBytes = 16_384;
        private static readonly Regex Code = new Regex("^[a-z][a-z0-9_.:-]{0,127}$", RegexOptions.CultureInvariant);
        internal static void Timestamp(long value, string parameter) { if (value < 0 || value > 253_402_300_799_999L) throw new ArgumentOutOfRangeException(parameter); }
        internal static void BusinessSource(string value, string parameter)
        { if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0) throw new ArgumentOutOfRangeException(parameter); }
        internal static void Diagnostic(string? value, string parameter) { if (value == null || !Code.IsMatch(value)) throw new ArgumentOutOfRangeException(parameter); }
    }
}
