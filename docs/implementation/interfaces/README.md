# CL-001 interface baseline

This baseline freezes responsibility, ownership and dependency direction for P1. It does not add operation schemas, feature implementations or claim Unity/runtime acceptance. Later tasks may add signatures inside these responsibilities only with a reviewed delta.

## Portable interface ownership

| Responsibility | Owning assembly | P1 baseline | Ownership and error rule |
| --- | --- | --- | --- |
| Semantic remote ports | `GamePlatform.Backend.Contracts` | One narrow interface per application use case; request and response types are semantic contracts, never URLs, HTTP status or serialized bytes. May reference immutable application-facing types from `Features.Contracts`. | Caller owns cancellation interest. A cancelled wait does not claim cancellation of a committed server effect. Remote failures are typed and fail closed. |
| Local transaction/session ports | `GamePlatform.Storage.Abstractions` | Narrow use-case sessions execute projection, sequence allocation and immutable outbox admission in one real transaction. May reference immutable feature contracts; SQL rows and native handles stay in the adapter. | Application use case owns transaction lifetime. Commit/rollback is explicit; disposal cannot report success for uncommitted work. No universal repository bag. |
| Immutable snapshots | `GamePlatform.Features.Contracts` | Account/app/view-scoped read models carry owner, generation, revision and freshness. They contain no mutable collection or transport/storage type. | Snapshot producer owns copies; consumers never mutate backing state. Missing or stale state is explicit and is not an empty-success substitute. |
| Command admission | `GamePlatform.Features.Contracts` for intent/result; `Storage.Abstractions` for durable admission | Intent uses semantic/provider identifiers plus platform-owned operation and stream identities. Durable admission validates owner and checked signed-64 sequence/revision values before atomically storing immutable bytes. | Admission success means local durability only. It never means server acceptance, reward grant or pull-checkpoint advancement. |
| Scope and lifecycle ownership | Consumer composition root, with portable narrow factories where needed | Infrastructure may outlive one application scope. Account/view services are constructed for an immutable owner context and disposed/quiesced before replacement. | No service locator, ambient current account or static mutable registry. Late results retain captured owner and generation and cannot publish into a replacement scope. |
| Wire DTOs | `GamePlatform.Wire.Contracts` | Versioned canonical protocol shapes only after backend schema review. Distinct from domain, feature snapshots and SQL rows. | Unknown/version/bounds behavior comes from the reviewed schema. P1 does not invent DTOs. |
| Byte transport and codec | `GamePlatform.Transport.Abstractions` | `IHttpExecutor` owns neutral request/response bytes; `IWireCodec` owns bounded encode/decode. Semantic ports compose these in outer adapters. | Buffers are caller-owned unless an API explicitly transfers ownership. Decode failure never falls back to JSON success. |
| Diagnostics | `GamePlatform.Diagnostics.Abstractions` | CL-014 may add `IStructuredAppLogSink`, immutable `AppLogRecord`/`AppLogContext`, and `SafeAppLog` while retaining `IAppLog.Write` and `NullAppLog`. | Context captures correlation and generation; no ambient account. Disabled checks precede field factories. Bounded fields redact arbitrary strings/objects and sensitive values; exceptions expose presence only. Sink failure cannot decide a transaction. |

## Frozen P1 dependency decisions

`Features.Contracts` remains BCL plus Core. `Backend.Contracts` and `Storage.Abstractions` may depend on `Features.Contracts` for semantic intents, results and immutable snapshots; this avoids duplicate remote/storage DTO families. The permission is narrow: it does not allow feature implementations, HTTP types, SQL rows or adapters inward.

The codec adapters retain their existing Core edge because the current fail-closed exception and future reviewed UUID/numeric conversions are Core-owned. `Wire.Contracts` remains independent. Codec assemblies may not define a second domain identity family.

Every runtime package is deny-by-default in `architecture.json`. A later dependency task must add an exact project/package allowance and its qualification evidence together. UnityEngine, Microsoft.Data.Sqlite, VContainer and UniTask remain forbidden in portable runtime projects.

## Current signature inventory and deltas

The P1 source still contains compatibility-shaped `PlatformId`, `PlatformResult<T>`, `IFeatureCapability`, byte-only `IBackendGateway`, `IPlatformStore`, `IHttpExecutor`, `IWireCodec` and `ProtocolVersion`. These signatures are inventory, not accepted production implementations. CL-002 owns canonical IDs/results, CL-003 owns reviewed wire shapes, CL-005 owns semantic HTTP composition, CL-007/008 own storage sessions/admission, CL-011 owns snapshots, and CL-014 owns the additive diagnostics API above.

Compared with M0, the architecture allowlist adds only `Backend.Contracts -> Features.Contracts` and `Storage.Abstractions -> Features.Contracts`. No project currently consumes either permission. The validator now checks actual direct and transitive project references, deny-by-default direct/locked runtime packages, catalog/manifest status agreement and correlated implementation evidence.

## Evidence-aware status transition

Feature states use `stubbed`, `planned`, `implemented`, `unverified` or `blocked`. `implemented` is accepted only when all of these agree:

1. `features.json`, the feature ledger and the owning task identify the same implemented/complete state.
2. Their evidence lists are byte-for-byte equal and point beneath `docs/implementation/evidence/<task-id>/`.
3. Each JSON report identifies the task and feature, pins a lowercase 40-character implementation commit, names existing source and test files, and records at least one passing command with a nonzero selected-test count.

This is repository evidence correlation, not independent proof that the reported runtime behavior is correct. Reviewers must inspect the implementation and raw report. Unity, native, device and peer gates stay unverified until their own evidence exists.

Rollback must revert the implementation, evidence/status entries and any architecture permission together. Leaving an unused broader dependency permission after rollback requires a new reviewed rationale.
