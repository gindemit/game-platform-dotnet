# GamePlatform.Core

Owns neutral IDs, failures and results. BCL-only; excludes game/provider/serializer/storage types. CL-002 adds canonical scoped identity, RFC UUID conversion and generation, checked numeric helpers and legacy result guards. Relevant gates: A01, A07. Tests: `GamePlatform.Tests/Compatibility`.

`PlatformId` preserves the consumer's ordinal semantic/provider strings and UTF-16
128-character bounds. `SemanticId` and `BackendNamespace` keep explicit semantic
roles. `AppId`, `PlatformUserId`, `OperationId` and `ClientStreamId` wrap a `Guid`
without implicit conversions across roles. They preserve issued RFC UUID versions
1–8 admitted by the approved `common.uuid` schema; new IDs use `UuidV7Generator`.
`UuidIdentity` converts exactly 16 RFC-order bytes and compares their unsigned byte
order. It never serializes `Guid.ToByteArray()` as wire order.

`UuidV7Generator` takes `IUnixMillisecondClock` and `IUuidRandomSource`; system UTC
and cryptographic entropy implementations are supplied. Calls serialize access to
these dependencies, validate the agreed timestamp bounds and clamp clock rollback
to the last successful timestamp within that instance. Each call obtains fresh
entropy. This gives probabilistic uniqueness, not global causal or monotonic
within-millisecond ordering. Generator state is not a durable sequence allocator.

`OwnerScope` is exactly backend/app/player. `ScopedOwnerContext` adds an opaque
semantic view key and nonnegative transient generation, without modifying durable
ownership. Constructors reject invalid/default component IDs; default structs
remain explicitly invalid. `PlatformNumbers` provides checked signed-64 addition,
positive sequence increments and bounded Unix milliseconds.

## Consumer migration mapping

The source authority is immutable MrSquare commit
`bc53723a8eec3521dd7bd8e05eaa726fca29795d`. INT-002 must switch all inventoried
callers and remove the corresponding legacy declarations together:

| Legacy MrSquare.Platform responsibility | Canonical target |
| --- | --- |
| PlatformId | GamePlatform.Core.PlatformId, same semantic string behavior |
| ProviderFailure | GamePlatform.Core.PlatformFailure, values 0–7 preserved; NotImplemented=8 |
| OperationResult&lt;T&gt; | GamePlatform.Core.PlatformResult&lt;T&gt;, including null success, default failure value and 512-character diagnostic bound |
| DataOwnership / StateRecord | GamePlatform.Features.Contracts, identical guards and numeric enum values |
| GameplayOutcome / IGameplayOutcomeSink | GamePlatform.Features.Contracts, all ten fields and copied ordinal metrics |
| ProgressEvent / IProgressObserver | GamePlatform.Features.Contracts, existing sequence/counter/amount semantics |

Other specialized team, commerce, query and provider contracts stay in the legacy
consumer until their feature-specific extraction. They may reference these
canonical types. No provider, remote service, mutable store, fake or universal
PlatformServices/Host is copied into the SDK. UI feature contracts and later
operation-named remote ports remain separate consumer boundaries.

The outcome retains the legacy 1000-metric, UTF-16 and validation-reference rules.
G1 wire admission has stricter limits and must explicitly reject an inadmissible
outcome while retaining its original content; it cannot truncate, invent content
versions or grant trust to a checkpoint signature. StateRecord is a compatibility
model, not a SQL row, wire DTO or grant/mutation API.

`verify_legacy_consumer.py` compiles the actual immutable consumer's 14 existing
Platform tests against these canonical types in a temporary desktop project,
removing exactly the mapped duplicate declarations and renaming result/failure
references. Consumer fakes stay in that temporary test project. This is source
compatibility evidence only. Actual import, editor/scene regressions, package
rollback, AOT and device acceptance belong to CL-013/INT-002 and remain unrun here.
No save key, MGD1 format, provider identifier or Unity file changes in CL-002.
