# Frozen schema-neutral foundation contracts

Status: CL-001 design freeze. These are the exact target names, signatures, ownership rules and dependency locations for CL-002/003/005/007/008/011 to implement. They are not declarations that the types already exist. [CURRENT_API_INVENTORY.md](CURRENT_API_INVENTORY.md) separately records the M0 API that exists today.

This file freezes no wire keys, MessagePack indexes, endpoint paths, provider SDK types, SQL rows, feature economics or operation-specific bodies. G1 supplies those operation details without changing the ownership and lifecycle contracts below. A signature delta requires review against CL-001 rather than timestamp precedence.

## Core scope and identity inputs

CL-002 supplies the distinct value types referenced here: `AppId`, `PlatformUserId`, `OperationId`, and `ClientStreamId`. Each is an immutable platform-owned UUID wrapper. `BackendNamespace` and `SemanticId` are bounded ordinal strings; `BackendNamespace` separates non-interchangeable backend/data realms, while `SemanticId` preserves provider IDs, content keys, opaque authorized view keys and other non-platform identifiers. Existing issued UUID versions are preserved where their role allows them; generation and RFC-byte rules remain CL-002/A01 work.

```csharp
namespace GamePlatform.Core
{
    public readonly struct OwnerScope : IEquatable<OwnerScope>
    {
        public OwnerScope(
            BackendNamespace backend,
            AppId appId,
            PlatformUserId userId);

        public BackendNamespace Backend { get; }
        public AppId AppId { get; }
        public PlatformUserId UserId { get; }
        public bool Equals(OwnerScope other);
    }

    public readonly struct ScopedOwnerContext : IEquatable<ScopedOwnerContext>
    {
        public ScopedOwnerContext(OwnerScope owner, SemanticId viewKey, long generation);

        public OwnerScope Owner { get; }
        public SemanticId ViewKey { get; }
        public long Generation { get; }
        public bool Equals(ScopedOwnerContext other);
    }
}
```

`OwnerScope` is exactly the durable backend/app/player database and outbox isolation tuple. `ScopedOwnerContext.ViewKey` is a server-authorized opaque semantic cache/read-authorization key; CL-001 does not mandate another distributed UUID. `Generation` is nonnegative transient local lifetime identity, not a durable database key or server revision. A view/visibility reset replaces the scoped read context but never changes command owner, stream identity or fingerprint and cannot strand pending commands. Neither type contains an access token, provider credential or ambient “current account.” Every account/view service captures one context at construction. Results keep that captured value after account replacement.

## Semantic remote-call boundary

`GamePlatform.Backend.Contracts` owns the common call context and outcome. A feature or sync consumer owns a narrow operation-named interface in the same assembly. No public generic gateway is registered as a service, and callers never pass URLs, headers, HTTP status codes, serializer options or byte arrays through these semantic ports.

```csharp
namespace GamePlatform.Backend.Contracts
{
    public readonly struct ProvisioningCallContext
    {
        public ProvisioningCallContext(
            BackendNamespace backend,
            AppId appId,
            Guid correlationId);

        public BackendNamespace Backend { get; }
        public AppId AppId { get; }
        public Guid CorrelationId { get; }
    }

    public readonly struct RemoteCallContext
    {
        public RemoteCallContext(ScopedOwnerContext owner, Guid correlationId);

        public ScopedOwnerContext Owner { get; }
        public Guid CorrelationId { get; }
    }

    public enum RemoteFailureKind
    {
        Offline = 0,
        Unauthorized = 1,
        MembershipDenied = 2,
        Conflict = 3,
        Invalid = 4,
        Unsupported = 5,
        RateLimited = 6,
        Unavailable = 7,
        Protocol = 8,
        Unknown = 9
    }

    public enum RemoteEffectState
    {
        NotSent = 0,
        Unknown = 1,
        ServerFinal = 2
    }

    public readonly struct RemoteFailure
    {
        public RemoteFailure(
            RemoteFailureKind kind,
            RemoteEffectState effectState,
            bool retryable,
            string diagnosticCode);

        public RemoteFailureKind Kind { get; }
        public RemoteEffectState EffectState { get; }
        public bool Retryable { get; }
        public string DiagnosticCode { get; }
    }

    public sealed class RemoteResult<T>
    {
        private RemoteResult(T? value, RemoteFailure? failure);

        public bool Succeeded { get; }
        public T? Value { get; }
        public RemoteFailure? Failure { get; }

        public static RemoteResult<T> Success(T value);
        public static RemoteResult<T> Error(RemoteFailure failure);
    }
}
```

Exact narrow port convention:

```csharp
public interface I<OperationName>Remote
{
    Task<RemoteResult<<OperationName>Response>> <OperationName>Async(
        RemoteCallContext context,
        <OperationName>Request request,
        CancellationToken cancellationToken);
}
```

The placeholder is a naming rule, not a literal generic interface. Each operation receives its own concrete interface and immutable semantic request/response types after G1. Account-owned operations use `RemoteCallContext`. Initial anonymous provisioning ports use the same method shape with `ProvisioningCallContext`; they cannot manufacture a player, view or generation before the server issues membership. No other port accepts the provisioning context. Cancellation ends caller interest only. `RemoteEffectState.Unknown` is mandatory when a mutation may have reached the server; cancellation, timeout and decode failure cannot report `NotSent` unless the transport proves no bytes were sent. `diagnosticCode` is a bounded code-defined symbol and contains no server message or payload. Authentication secrets are injected into the outer HTTP adapter and never enter either context, diagnostics or storage projections.

G1 remains authoritative for the concrete operation names and body fields for provision, bootstrap pages, push/results, progression intent, private pull and reset. It also decides which operations carry `OperationId`/`ClientStreamId`, fingerprint inputs, null/default rules and version fields. Those decisions fill the request/response types; they do not change this port/context/result ownership.

## Local transaction and session borrowing

`RemoteResult<T>.Success` rejects null values; `Error` rejects invalid enum values and an empty/invalid diagnostic symbol. Retryability is an explicit server/transport classification: `ServerFinal` failures are non-retryable; a later safe read/replay is a new call, not a retryable classification for the finalized mutation. Correlation IDs in both contexts must be nonempty and process-local.

`GamePlatform.Storage.Abstractions` owns the real transaction lifetime. Application use cases invoke `ILocalUnitOfWork.ExecuteAsync`; storage collaborators borrow the supplied `ILocalTransaction`. There is no public `CommitAsync`, so a borrower cannot independently commit. Returning successfully from the callback asks the unit of work to commit. An exception or observed cancellation before commit rolls back. Once commit succeeds, later caller cancellation cannot replace the durable success/identity with a cancellation result. Commit failure propagates a typed failure. Implementations reject use after callback completion, and callbacks perform no external network work.

```csharp
namespace GamePlatform.Storage.Abstractions
{
    public enum LocalStoreFailureKind
    {
        OwnerMismatch = 0,
        ForeignTransaction = 1,
        TransactionExpired = 2,
        Conflict = 3,
        Overflow = 4,
        Unavailable = 5,
        Corrupt = 6
    }

    public sealed class LocalStoreException : Exception
    {
        public LocalStoreException(
            LocalStoreFailureKind kind,
            bool retryable,
            string diagnosticCode,
            Exception? innerException = null);

        public LocalStoreFailureKind Kind { get; }
        public bool Retryable { get; }
        public string DiagnosticCode { get; }
    }

    public interface ILocalTransaction
    {
        OwnerScope Owner { get; }
    }

    public interface ILocalUnitOfWork
    {
        Task<T> ExecuteAsync<T>(
            OwnerScope owner,
            Func<ILocalTransaction, CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken);
    }
}
```

Concrete projection, checkpoint, sequence and feature stores remain narrow consumer-boundary interfaces. Their mutation methods take `ILocalTransaction transaction` first and never open/commit another transaction. Query-only methods do not accept a transaction unless consistency with an active use case requires it. SQL connections, commands, rows and native handles never cross this boundary. Storage policy failures use `LocalStoreException`; cancellation uses `OperationCanceledException` with the caller token. `DiagnosticCode` is bounded/code-defined and never contains SQL, paths or private values.

An implementation validates reference identity of its own transaction object; a transaction from another store instance, owner or expired callback fails before mutation. `ILocalTransaction` is a capability token, not a service locator: it exposes only `Owner`, and repositories must not downcast it to discover unrelated services.

## Immutable scoped snapshots

`GamePlatform.Features.Contracts` owns snapshot state and query seams. `T` is a feature-specific immutable reference-type read model. Collections inside `T` are immutable/copies. Revision uses checked signed `long`; revision `0` is permitted only when the feature contract explicitly defines an unversioned/initial state.

```csharp
namespace GamePlatform.Features.Contracts
{
    public enum SnapshotFreshness
    {
        Missing = 0,
        Stale = 1,
        Current = 2
    }

    public sealed class FeatureSnapshot<T> where T : class
    {
        public FeatureSnapshot(
            ScopedOwnerContext owner,
            long revision,
            SnapshotFreshness freshness,
            T? value);

        public ScopedOwnerContext Owner { get; }
        public long Revision { get; }
        public SnapshotFreshness Freshness { get; }
        public T? Value { get; }
    }

    public interface IFeatureSnapshotReader<TQuery, TSnapshot> where TSnapshot : class
    {
        FeatureSnapshot<TSnapshot> Read(ScopedOwnerContext owner, TQuery query);
    }
}
```

`Missing` requires `Value == null`; `Current` requires a value; feature contracts decide whether `Stale` may retain the last value. A reader never silently substitutes an empty collection for missing state. Query types include every result-shaping input and contain no mutable or transport/storage type. Refresh remains a separate asynchronous use-case method so a synchronous read cannot hide I/O.

## Durable command admission

Feature/application code produces semantic intent. CL-003 defines the deterministic schema-neutral semantic body and fingerprint algorithm; CL-004 later encodes the transport DTO. Command admission therefore has no dependency on the transport codec. `GamePlatform.Storage.Abstractions` owns the durable draft and admission port because sequence allocation, final fingerprint computation and outbox insertion are local transactional effects. The draft constructor defensively copies input bytes. It exposes length/copy methods only, so no backing array can escape through `ReadOnlyMemory`/`MemoryMarshal`.

```csharp
namespace GamePlatform.Storage.Abstractions
{
    public sealed class CommandDraft
    {
        public CommandDraft(
            OwnerScope owner,
            OperationId operationId,
            ClientStreamId streamId,
            string operationKind,
            int schemaVersion,
            int fingerprintVersion,
            ReadOnlySpan<byte> semanticBody);

        public OwnerScope Owner { get; }
        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public string OperationKind { get; }
        public int SchemaVersion { get; }
        public int FingerprintVersion { get; }
        public int SemanticBodyLength { get; }
        public void CopySemanticBodyTo(Span<byte> destination);
    }

    public readonly struct CommandAdmission
    {
        public CommandAdmission(
            OperationId operationId,
            ClientStreamId streamId,
            long sequence,
            long localRevision);

        public OperationId OperationId { get; }
        public ClientStreamId StreamId { get; }
        public long Sequence { get; }
        public long LocalRevision { get; }
    }

    public interface ICommandAdmissionStore
    {
        Task<CommandAdmission> AdmitAsync(
            ILocalTransaction transaction,
            CommandDraft command,
            CancellationToken cancellationToken);
    }

    public interface ICommandFingerprint
    {
        int GetFingerprintLength(int fingerprintVersion);
        void Compute(
            OwnerScope owner,
            OperationId operationId,
            ClientStreamId streamId,
            long sequence,
            string operationKind,
            int schemaVersion,
            int fingerprintVersion,
            ReadOnlySpan<byte> semanticBody,
            Span<byte> destination);
    }
}
```

`AdmitAsync` validates `transaction.Owner == command.Owner` and that the stream belongs to that durable backend/app/player scope. It allocates the next checked positive sequence, invokes the injected pure `ICommandFingerprint` with the complete immutable command envelope including that sequence, inserts immutable semantic body plus final fingerprint into the outbox, and returns identifiers inside the caller-owned transaction. View key and transient generation are deliberately absent from command ownership and fingerprint inputs. Fingerprint computation performs no network or transport serialization and comes from the G1-approved CL-003 algorithm. The caller performs projection mutation through its narrow store using the same transaction. Commit makes projection, local revision/sequence and outbox visible together; rollback exposes none.

Reusing an `OperationId` with identical owner/stream/sequence-independent draft semantics returns the already admitted identity only under the reviewed idempotency policy; changed owner/stream/schema/body is a typed conflict. The exact stored fingerprint bytes/algorithm are CL-003 details, but successful admission never means server acceptance, receipt finalization, reward grant or pull-checkpoint movement. A push acknowledgment never changes the pull checkpoint through this interface.

## Ownership and error invariants

- Constructors null-check dependencies and start no I/O. Infrastructure owns unit-of-work/transport implementations; application scope owns its captured services; account/view replacement quiesces old writers before disposal.
- Every asynchronous API accepts a caller `CancellationToken`; cancellation before commit rolls back, while cancellation observed after commit preserves the durable result. Cancellation never infers server rollback.
- Checked overflow, owner mismatch, foreign/expired transaction, malformed bounds and changed-body duplicate fail before commit. No method returns a success-shaped default for these cases.
- Domain/feature models, wire DTOs, SQL rows and game checkpoint/save types remain distinct. MGD1 and PlayerPrefs formats are not inputs to these contracts.
- Diagnostics may observe captured correlation/generation but cannot determine transaction results and never receives tokens, bodies, fingerprints, receipts or private payloads.

## Frozen versus G1-blocked

Frozen here: assembly owners, type names above, member signatures, separate pre-account/account-owned call contexts, narrow-port naming convention, exact durable owner tuple versus transient view context, transaction borrowing, snapshot missing/stale/current semantics, defensive immutable command buffers, post-sequence pure fingerprinting without view inputs, cancellation/effect uncertainty, owner capture and commit boundaries.

Blocked on G1: concrete operation interface names; request/response fields; wire names/indexes/types/null/default/bounds; operation/version enums; fingerprint canonical bytes; protocol negotiation; exact cursor/page/group/error bodies. Adding those reviewed types completes the operation surface without reopening these foundation contracts.
