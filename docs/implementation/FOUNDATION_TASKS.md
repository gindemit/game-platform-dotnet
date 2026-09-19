# Executable SDK foundation cards

Planning baseline: `f446101574e11c9885583ea184af583303b74ba1` (SDK), `a3dc3f00aea334cdf538cc34d49c2e8a92b8cbd9` (consumer), `66e40558045cbe5c314119022a55ced46ad9d04e` (backend). All cards are **planned**, not implemented by this document.

Read this file with [the manifest](execution-manifest.json), [audit](AUDIT.md), and [execution runbook](RUNBOOK.md). Each card inherits the delivery contract below; its individual paragraph supplies the task-specific additions. Minimal reading means root and applicable nested AGENTS.md, the named source sections, the card, and frozen interface files—not all three repositories. `DN`, `SQ`, `PEER`, `UE`, `UP`, and `UA` expand to exact commands and environment prerequisites in RUNBOOK.md. New test/harness names below are proposed, never claims that they already exist.

## Common card delivery contract

Use `impl/<task-id>-<short-name>` in an isolated worktree. Only edit manifest-owned paths; shared project/lock/assembly/migration-registry changes go through the named coordinator. A task-local test directory is proposed unless already present. Before implementation, record current HEAD, dirty files, API inventory, accepted interfaces, and baseline test results. Preserve unrelated changes. No force push, publishing, deployment, production migration, account merge or secret change.

Deliver a reviewable PR with implementation, positive/negative/race tests, exact command/tool versions and exit codes, logs under the manifest evidence path, limitations, rollback/forward-recovery notes, updated module README and `docs/IMPLEMENTATION_STATUS.md` patch. The coordinator, not concurrent workers, merges status/manifest edits. Never mark a stub or skipped test as implemented. Review must inspect code and evidence independently, including actual references and disposal ownership. Passing DN alone cannot make a feature integrated. Public APIs, SQL rows, wire DTOs and game checkpoints remain separate. All new API names below describe responsibilities; freeze actual signatures before dependants start.

Every card's **Prompt** is copyable together with this common contract and its manifest row. Worker completion reports must name the exact files changed, tests run, unrun gates, remaining unsupported behavior, peer contract revision, and any proposed contract delta.

<a id="cl-001"></a>
## CL-001 — Interface baseline and honest status transition

**Scope/value:** SDK; M1 / MrSquare A; P0; PlatformCoordinator. Enable real implementation without weakening architecture. No feature implementation or package upgrade.

**Read/evidence:** `architecture.json`, `features.json`, `scripts/validate.py`, `docs/ARCHITECTURE.md`, `docs/DEPENDENCIES.md`, Unity AssemblyResponsibilities/assembly-map and acceptance A10/A13/DI09. The scaffold validator currently requires all sixteen statuses to remain stubbed; several SDK edges differ from the target Unity map.

**Dependencies/ownership:** independent. Own the manifest-listed shared policy files, their companion `scripts/test_validation.py` regression tests and proposed `docs/implementation/interfaces/`; no backend canonical edits or Unity runtime edits. Coordinate any central props, solution or lock changes rather than letting feature workers edit them.

**Steps:** (1) Inventory current signatures and direct/transitive references. (2) Publish a reviewed interface table for semantic remote ports, local transaction/session ports, immutable snapshots, command admission and ownership. (3) Reconcile exact graph differences, including backend/storage-to-feature contracts and codec-to-core; allow only justified edges. (4) Replace scaffold-only status checks with evidence-aware allowed transitions while retaining missing-module, boundary, lock and contract-pin checks. (5) Freeze signatures and error/ownership rules for independent tasks; record changes by delta, not timestamp precedence.

**Tests/commands:** DN plus Python validator tests. Arrange a real implemented module with evidence and a fake success/no evidence; accept only the first transition. Reject forbidden package/transitive edges, missing modules, unknown statuses and changed contract hashes. Preserve all negative validator tests.

**Acceptance/delivery:** reviewed interface/map delta and tested validator transition, not sixteen newly marked implementations. API review is required before CL-002/003/007 proceed. Roll back policy and implementation together; never leave a broadened allowlist without the matching rationale.

**Prompt:** Implement CL-001 only. Make scaffold validation capable of truthful incremental implementation while preserving strict dependency and evidence checks; deliver the frozen interface baseline for the other workers.

<a id="cl-002"></a>
## CL-002 — One canonical core and compatibility-preserving extraction

**Scope/value:** SDK; M1/A; P0. Establish portable IDs/results/outcomes without an incompatible second platform. No Unity behavior change in this PR.

**Read/evidence:** Core source, Features/Backend contract stubs, `integration/unity/EXTRACTION.md`, Unity PlatformContracts.cs/PlatformRuntime.cs/FakeProviders.cs and migration matrix. Current SDK PlatformId is an ordinal string; current game has richer result guards, enums and generic outcomes. Typed UUIDs are target behavior, not already implemented.

**Dependencies/ownership:** CL-001 and INT-001 inventory. Own Core and proposed compatibility contract/test subdirectories. ConsumerIntegrationOwner coordinates the later Unity switch; do not change game callers from this worker.

**Steps:** (1) Freeze one owner/name for each legacy responsibility. (2) Preserve semantic/provider strings and legacy enum numeric/error behavior; introduce distinct typed Guid wrappers only for platform-owned UUID roles. (3) Implement RFC-order comparison/conversion and a Unity-compatible injected UUIDv7 generator, accepting issued v4 where allowed; handle clock rollback without claiming causal ordering. (4) Move generic outcome/progress contracts without puzzle references. (5) Separate UI-facing services from remote providers. (6) Mark any temporary outer bridge with callers/removal gate; it forwards to canonical types and owns no duplicate mutable state.

**Tests/commands:** DN. Golden legacy invalid/default ID, 128-character bound, invalid enum, ownership/revision, copied metrics and result failure tests; v4/v7/RFC byte vectors, int64 checked overflow and timestamp bounds. Rebuild scaffold consumers against the single canonical API. Do not use Guid.CreateVersion7 merely because build SDK 9 supports it.

**Acceptance/delivery:** reviewed API diff plus compatibility vectors used by INT-002; no game/Unity reference in portable core. Compile-only import is not game adoption. Rollback uses the previous coordinated bundle and callers, never converts saved IDs arbitrarily.

**Prompt:** Implement CL-002 against the migration inventory. Preserve legacy semantics while establishing one canonical SDK contract set; do not reinterpret semantic content keys as UUIDs.

<a id="cl-003"></a>
## CL-003 — First operation-schema freeze and reviewed mirror pin

G1 epoch 2: schema/mirror review delivered as `0.2.0-core-schema.1`; see
[review and mappings](evidence/CL-003/G1-epoch2/README.md). The attribute-free DTO
subpart in Steps remains P2 work, so CL-003 is partial/in_progress. Do not infer
runtime compatibility or skip this delivery before dependent codecs.

**Scope/value:** SDK client contract owner; M1/A; P0. Turn draft primitive policy into a usable peer agreement. No independent backend protocol fork.

**Read/evidence:** `contracts/README.md`, policy/vectors/snapshot, pin/sync scripts, Wire.Contracts, CONTRACT_HANDOFF.json; PlatformContracts identity, ordering, bootstrap and sync sections. Baseline has only draft policy/primitive vectors, no complete operation DTO set.

**Dependencies/ownership:** CL-001; external PEER-CORE. Client owner may update reviewed mirror and Wire.Contracts; only the separately assigned BackendContractOwner edits canonical backend contracts. This task can prepare requests while blocked, but cannot claim freeze from examples.

**Steps:** request canonical schemas for auth/provision membership, complete bootstrap paging, profile/progression commands, ordered push/results, private pull/reset, version/error semantics and canonical-semantic fingerprints. Resolve absent/null/unknown-required fields, duplicate map keys, sequence-finalization and snapshot-expiry rules. Require schema/fixture versions and digests, field-by-field C#/TS mapping, additive/breaking compatibility assessment and peer review. Copy via existing sync tooling, verify hashes, then produce attribute-free DTOs. Pin exact reviewed backend commit; do not track moving main.

**Tests/commands:** Run schema/semantic-vector validation, mirror pin verification and negative schema/drift tests for G1. Specify paired canonical fingerprint expectations now. Executable both-direction PEER exchange and equal runtime fingerprints follow at G2 after CL-004/016 and BE-005/015; they are not prerequisites to schema approval. Runtime cursor behavior remains unverified until implemented.

**Acceptance/delivery:** canonical commit + every artifact hash + schema/fixture versions + field mappings + actual reviewers + reviewed mirror commit and compatibility policy. Runtime implementation revisions remain null/unverified until G2 and do not block G1. Gate approval alone does not complete CL-003: its schema-only deliveries, DTO mappings and tests remain required. A02 still requires actual peer codec exchange. No JSON decode fallback. Any post-freeze change needs a delta and coordinated repin.

**Prompt:** Implement CL-003 as the client-side contract freeze owner. Request missing schemas from the backend owner and fail closed until their reviewed canonical artifacts exist.

<a id="cl-004"></a>
## CL-004 — Bounded MessagePack codec and real cross-language fixtures

**Scope/value:** SDK codec adapter; M1/D; P0. Preserve exact interoperable values. No serializer attributes in domain/SQL types and no JSON production fallback.

**Read/evidence:** Serialization.MessagePack stub, Wire.Contracts, Transport.Abstractions, adopted codec policy and frozen CL-003 DTOs. Current codec throws unavailable.

**Dependencies/ownership:** CL-002/003. Own MessagePack adapter and proposed serialization tests; dependency pin/project edits are PackagingOwner/PlatformCoordinator-owned. Primitive tests may be prepared before operation freeze, but A02 cannot pass without a real peer.

**Steps:** pin reviewed MessagePack-CSharp and transitives at adapter boundary; implement named-field explicit/generated AOT-safe formatters or surrogates; implement UUID bin16 RFC order, checked signed int64 and timestamp bounds; enforce byte/depth/collection limits and duplicate-key rejection; validate required/optional unknown fields; preserve schema-defined absent/null semantics. Never enable typeless or runtime-only code generation. Treat decode failure after a mutation as uncertain, not permission to resend differently.

**Tests/commands:** DN + PEER. C# encode→real TypeScript decode and reverse for v4/v7, min/max long, compact integer representations, >2^53 values, timestamp ends, absent/null, unknown fields, malformed bin lengths, invalid variant, duplicate keys, nested/oversize payloads. Include equivalent semantic fingerprints from different permitted encodings.

**Acceptance/delivery:** raw fixture bytes, canonical decoded expectations, both executable peer logs and version locks; same vectors must later pass UA. Self-roundtrips and Python-only vectors are insufficient. Preserve the previous codec/bundle until compatible repinning succeeds.

**Prompt:** Implement CL-004 using the frozen schemas. Prove real C#↔TypeScript MessagePack interoperability and hostile-input bounds without leaking serializer types inward.

<a id="cl-005"></a>
## CL-005 — Semantic HTTP providers, not a byte-only facade

**Scope/value:** SDK HTTP orchestration; M1/D; P0. Provide testable network behavior with typed failures. No UnityWebRequest in portable provider code.

**Read/evidence:** current IBackendGateway, IHttpExecutor, IWireCodec and Http stub; AssemblyResponsibilities transport boundaries; frozen operation schemas. Current executor accepts only bytes and cannot express method, URL, status or headers.

**Dependencies/ownership:** CL-003. Own proposed Remote contracts, Transport.Abstractions and Transport.Http; shared contract lock serializes public signatures.

**Steps:** define neutral request/response models with method/path/headers/status/bounded body and cancellation; inject executor/codec/auth-session boundary and logical backend configuration. Implement schema-specific providers with DTO mapping, Content-Type/Accept/version checks and explicit error classification. Coordinate one auth refresh per account/session and bounded retries; retain operation/stream/body identity. Reads and durable mutations have different retry policies. Reject host/namespace confusion and wrong-owner responses. Do not log credentials, receipts or full private bodies.

**Tests/commands:** DN with scripted executor: timeout before send, uncertain timeout after send, 401 refresh races, 403, 409 changed-payload conflict, 429 Retry-After, 5xx, wrong content type/version, truncated/oversize response, caller cancellation and account replacement. Assert no mutation is automatically replayed as JSON and no response advances a pull cursor.

**Acceptance/delivery:** endpoint/error mapping table tied to CL-003 plus request traces with redaction; Unity executor remains a separate INT-004 gate. Preserve safe unavailable results for unsupported operations.

**Prompt:** Implement CL-005 as semantic providers over injected neutral executor/codec ports; make cancellation, renewal and uncertain mutation outcomes explicit.

<a id="cl-006"></a>
## CL-006 — SQLite candidate qualification and acquisition decision

**Scope/value:** PackagingOwner; M2/C; P0. Decide a reproducible driver/native source path before writing persistence. No speculative package upgrades or claim that desktop proves Android.

**Read/evidence:** dependency policy, `integration/unity/EXTRACTION.md`, actual Unity manifest/lock/settings and native imports. `gilzoide/unity-sqlite-net` is the documented initial choice/candidate, not installed in either consumer or SDK.

**Dependencies/ownership:** independent qualification; own proposed dependency decision/qualification directories only. Any package pin is reviewed with PlatformCoordinator; do not add native code to Core.

**Steps:** inspect the candidate's exact pinned source, license and managed/native build layout using current official upstream material. Record its portable API surface, P/Invoke names and Unity-specific pieces. Prove a small real desktop open/transaction/reopen probe. Select one distribution path: a versioned managed DLL/native bundle assembled by the SDK packaging pipeline; do not additionally install the same managed SQLite library through UPM. Inventory Windows/macOS Editor and Android ARM64 native inputs, import CPU/OS settings, checksums, notice files, AOT requirements and thread ownership. If the candidate cannot satisfy portable adapter boundaries, record a bounded alternative ADR before changing policy; do not silently substitute Microsoft.Data.Sqlite.

**Tests/commands:** DN/SQ probe once introduced; native binary metadata and license checks. Device execution belongs to CL-015/INT-009 and remains pending. Missing binaries/runner are explicit qualification blockers.

**Acceptance/delivery:** exact source/package revision, dependency graph, reproducible acquisition instructions and desktop probe evidence; conditional native matrix with no unsupported target promises. Remove experimental artifacts on rollback, not player databases.

**Prompt:** Implement CL-006 qualification, preserving the existing SQLite direction. Produce a reviewed pin/distribution decision and real desktop probe, with Android evidence explicitly deferred to its device gate.

<a id="cl-007"></a>
## CL-007 — Real serialized SQLite executor and scoped migrations

**Scope/value:** StorageOwner; M2/C; P0. Own one real connection/writer and crash-safe schema evolution. No generic unrestricted repository framework.

**Read/evidence:** Storage.Abstractions/UnavailableSqliteStore, client storage sections 8–11, SQL example, lifecycle quiescence rules. Existing SQL example is not an installed migration.

**Dependencies/ownership:** CL-001/002/006. Own storage abstractions, executor, migrations and real SQLite tests; registry/project changes serialize through StorageOwner.

**Steps:** implement explicit database/session leases scoped to logical backend/app/account; serialize work off Unity main thread; configure and verify foreign keys and reviewed journal/busy policy. Define a transaction-bound session borrowed by repositories and game writers, not an independent nested commit. Implement scope/version/id/checksum migration journal and required ordering. Commit migration effects and marker atomically; reject changed checksums/newer incompatible schemas. Quiesce admitted work before disposal and forbid replacement access on timeout. Create only foundational tables required by subsequent slices.

**Tests/commands:** DN + SQ with the selected native library: parallel writers serialize; transaction rollback leaves no partial schema; crash before/after marker; reopen; disk/write/locked failure; invalid owner; cancellation while queued versus in an active transaction; disposal during work; foreign-key enforcement. Use controlled barriers, not sleeps.

**Acceptance/delivery:** actual native SQL logs and migration checksums, connection/writer count assertions, fresh/upgrade/reopen tests. In-memory mocks do not qualify. Forward recovery and backup handling must be documented before destructive changes.

**Prompt:** Implement CL-007 with one serialized real SQLite writer, explicit leases and immutable scoped migrations; prove quiescence and transactional recovery.

<a id="cl-008"></a>
## CL-008 — Atomic local projection, sequence and immutable outbox

**Scope/value:** StorageOwner; M2/C; P0. Never lose a completed local action's synchronization identity. No HTTP calls inside the transaction.

**Read/evidence:** CL-007 session contract; PlatformContracts §2; DataAndRuntimeFlows §2; SQL outbox triggers. No current SDK durable outbox exists.

**Dependencies/ownership:** CL-003/007; own proposed Outbox implementations/ports and durability tests. Game-specific rows remain in MrSquare's adapter but borrow this exact transaction.

**Steps:** persist database/stream identity and owner tuple; require Ready before owned mutation. In one transaction dedupe business run, allocate checked sequence, update provisional projection and insert immutable versioned semantic command/fingerprint. Separate body identity from scheduling/lease/result fields. Preserve assigned operations through cancellation, retries and restart. Recover abandoned in-flight leases without a second sender. Model terminal rejection as finalization, not endless retry. Define explicit cloned/restored-storage recovery against server watermarks; never reset or relabel attempted streams to hide gaps.

**Tests/commands:** DN + SQ: crash at every write boundary, duplicate completed run with same/different attempted body, sequence overflow, disk full, repeated reopen, concurrent duplicate callbacks, clone with stale counter, mixed owner/app/stream, changed immutable fields and non-Ready admission. Assert either all three effects or none; inspect persisted rows after process termination.

**Acceptance/delivery:** fault matrix with pre/post-commit database snapshots, stable operation bytes/fingerprint/sequence and meaningful save failure. A SaveProgress followed by separate Enqueue implementation fails review. Rollback preserves pending envelopes and schema compatibility; no deleting old saves.

**Prompt:** Implement CL-008 as a transaction-bound atomic mutation API. Prove that projection, sequence and immutable outbox survive crashes together, with explicit clone/gap recovery.

<a id="cl-009"></a>
## CL-009 — Ordered push and durable outcome finalization

**Scope/value:** SDK Sync; M3/D; P0. Submit each retained command safely and unblock terminally rejected sequences. No exactly-once-delivery claim.

**Read/evidence:** Sync stub, CL-005 semantic port, CL-008 durable store and canonical stream contract. RunOnceAsync currently reports NotImplemented.

**Dependencies/ownership:** CL-004/005/008; own proposed Sync/Push and tests; one sync-core integration owner.

**Steps:** one sender per account/app/stream, contiguous bounded batches, persisted lease/attempt metadata, bounded jitter/backoff via injected clock/randomness. Distinguish pending, in-flight, blocked-auth, retryable and terminal acceptance/rejection. Persist terminal receipt before notifying; finalize rejected sequences so later valid commands may proceed. Retain accepted overlays until ordered pull incorporates them. On gaps query/recover the same stream; unsupported commands follow frozen policy. Cancellation closes caller interest, not server effects.

**Tests/commands:** DN/SQ: lost response after server commit, duplicate retries, changed-body duplicate, expected-sequence gap, terminal rejection then next success, transient failure stops batch, renewal, mixed-stream rejection, restart lease recovery and two senders racing. Assert stable IDs/bodies, bounded retries and unchanged pull cursor on every ACK.

**Acceptance/delivery:** deterministic race and database evidence; fake server tests are unit evidence only, PEER/live acceptance is INT-010. Retry policy and manual recovery diagnostics must expose blockers instead of resetting accounts.

**Prompt:** Implement CL-009 ordered durable sending and outcome finalization. Preserve command identity under uncertainty and never advance the pull cursor from a push response.

<a id="cl-010"></a>
## CL-010 — Coherent pull, complete bootstrap and reset recovery

**Scope/value:** SDK Sync/StorageOwner; M2–M3/C–D; P0. Install a consistent private view without dropping pending work.

**Read/evidence:** canonical private-feed/snapshot contract, client storage bootstrap and view-membership rules; CL-007/008 transactions. No runtime pull or bootstrap exists.

**Dependencies/ownership:** CL-004/005/008. Own proposed Pull, storage Sync and tests; acquire sqlite-writer and sync-core locks.

**Steps:** treat cursors as opaque authorized tokens; request fixed-watermark pages, validate complete groups and required entity versions. Atomically apply each group/page projection and continuation; never hydrate earlier revisions from unconstrained live state. Stage all bootstrap collections under one materialized boundary; atomically install view/checkpoint/Ready only after completeness. On retention/visibility reset, replace the correct confirmed view, preserve outbox and rebuild overlays. Distinguish view removal from entity tombstone and avoid regressing shared rows from older app pulls. Reject unknown required changes/expired sessions without checkpoint advancement.

**Tests/commands:** DN/SQ/PEER: crash before/after page commit, duplicate pages, invisible-prefix advancement only from server cursor, changing visibility between pages, >one-page inventory, unknown required kind, oversized group, lost snapshot page, reset with accepted/pending/rejected commands, two app views and stale revisions. Assert old checkpoint and data remain recoverable after failure.

**Acceptance/delivery:** fixed-boundary fixture transcripts and staged/install crash evidence. Summary+high cursor is not bootstrap. Real PostgreSQL commit-order proof is backend-owned A04 and must be linked rather than inferred from mocks.

**Prompt:** Implement CL-010 complete bootstrap and atomic private-feed reconciliation, including retention reset and pending-overlay preservation; do not invent per-domain cursors.

<a id="cl-011"></a>
## CL-011 — Feature snapshots, durable state and disposable query caches

**Scope/value:** SDK shared feature policy; M3/C; P1. Expose meaningful freshness instead of fake empty success.

**Read/evidence:** feature stubs, DataAndRuntimeFlows §1, PlatformContracts cache/extensibility sections, CL-007 storage. No current freshness engine exists.

**Dependencies/ownership:** CL-002/007; own proposed Shared/Snapshots/Cache and tests; storage lock required. No feature-specific server authority or reactive framework.

**Steps:** immutable snapshots distinguish missing/available/stale/pending/unavailable/error and last confirmed revision/refresh. Define canonical complete namespace/app/account-or-public/query keys including visibility/assignment/version/locale/filters/page. Durable private projections, receipts and outbox are never TTL caches. Coalesce refreshes with shared Task; one cancelled waiter cannot cancel other subscribers. Compare captured owner/generation and revision on publication. Preserve supported unknown extension fields and reject invalid/deep/oversized payloads.

**Tests/commands:** DN/SQ: cache eviction cannot erase outbox; two accounts/apps/queries cannot collide; stale result cannot regress a newer revision; not-found differs from offline; concurrent refresh single remote call; waiter cancellation; account switch; extension RMW preserves unknown fields.

**Acceptance/delivery:** cache-key vectors and snapshot state table with behavior under offline access. No premature universal entity/EAV store. Rollback may invalidate disposable caches, never durable records.

**Prompt:** Implement CL-011 scoped immutable snapshots and bounded query-cache mechanics, leaving freshness/authority decisions with each feature.

<a id="cl-012"></a>
## CL-012 — Pure minimal navigation and presenter lifetimes

**Scope/value:** SDK navigation/presentation; M2/B; P1. Support the first Home→play→result→Next→Home and caller-scoped Profile flow. Not adoption of the entire five-tab proposal.

**Read/evidence:** Navigation/Presentation stubs; DataAndRuntimeFlows UI/lifetimes; adopted LC09/DI01/03/07; shell proposal only as nonbinding background.

**Dependencies/ownership:** CL-002. Own Navigation and proposed presentation shared/test subdirectories. No game screens, Unity objects or account service locator.

**Steps:** define typed routes/arguments/results and caller return context; separate route history, modal ownership and deferred popup policy. Bound navigation state and cancel stale loads. Presenters borrow injected account services and own subscriptions/view leases. Close/Back resolves one owner once; Next replaces level scope rather than runtime. Model pending/confirmed/save-failed results explicitly. Keep optional tab configuration out of fixed platform enums.

**Tests/commands:** DN: Home/play/result/Next/Home, Profile from two different callers, Back while modal active, cancelled load finishing late, repeated Bind/Unbind, stale account generation, unavailable route and disposal of owned—not borrowed—resources. No scene required for pure tests.

**Acceptance/delivery:** deterministic navigation state tests plus presenter/view-port examples for INT-008. Fake screens do not constitute integrated feature UI. Preserve game controls/visual design at the later binding boundary.

**Prompt:** Implement CL-012 only for the minimal accepted flow and reusable ownership primitives; do not build a speculative universal window framework.

<a id="cl-013"></a>
## CL-013 — Early reproducible managed SDK artifact

**Scope/value:** PackagingOwner; M1/B; P0. Enable the first real consumer import before all features exist. No package publication.

**Read/evidence:** existing package-sdk.py, central props/locks, EXTRACTON inventory, LICENSE-NOTICE and Unity asmdefs. Current script copies internal DLLs only and declares native libraries absent.

**Dependencies/ownership:** CL-001/002. Own packaging script and proposed package metadata directories under the packaging/shared locks.

**Steps:** preserve netstandard2.1/C#9 and pinned build SDK. Build one coordinated SDK version/commit artifact with internal DLLs, symbols where generated, licenses, SHA inventory, dependency graph and explicit feature capability list. Mark native/codec capabilities absent when not bundled. Provide deterministic clean-build verification and an import plan listing precompiled references rather than auto-referencing every DLL. Pin the consumer to exact artifact hashes; no moving-branch acquisition or parallel NuGet+UPM copy of the same runtime library.

**Tests/commands:** DN then existing `python scripts/package-sdk.py`; compare two clean output inventories and assemblies, reject duplicate assembly names/missing references. Save package SHA and actual assembly target metadata. Unity import is INT-002, not inferred here.

**Acceptance/delivery:** downloadable/buildable unpublished bundle and rollback manifest for the previous bundle. Empty adapters remain unavailable. This minimal artifact is an import proof, not mature reuse or native acceptance.

**Prompt:** Implement CL-013 so the canonical core can be imported early and reproducibly by MrSquare, with exact hashes, references and honest capability metadata.

<a id="cl-014"></a>
## CL-014 — Small safe diagnostics boundary

**Scope/value:** SDK diagnostics; M1/B; P1. Make failures observable without changing transaction outcomes. No mandatory ZLogger, Microsoft logging, ZString or reactive packages.

**Read/evidence:** actual IAppLog/NullAppLog, AssemblyResponsibilities logging section and security redaction policy. Existing Write method lacks some target enabled/event/structured-field semantics.

**Dependencies/ownership:** independent; own Diagnostics.Abstractions and proposed tests. Contract edits must stay compatible or include reviewed caller migration.

**Steps:** implement only needed enabled check, stable event/category/severity and bounded structured fields with a null sink. Keep captured owner/generation correlation on records, not a static current account. Redact tokens, secrets and receipt/private payloads; bound queues and avoid formatting disabled events. Sink failures cannot decide transaction success. Unity output adapter is INT-004; optional provider adoption requires separate measured justification.

**Tests/commands:** DN: disabled log avoids expensive factory; throwing sink cannot abort committed work; overlong fields bounded; sensitive tokens redacted; delayed A event remains A after B activation; null sink works without framework. Measure real allocations before any claim.

**Acceptance/delivery:** API rationale, redaction vectors and failure tests. Do not claim zero allocations or introduce a large logging framework. Rollback remains null-safe.

**Prompt:** Implement CL-014 as a small explicit diagnostics port and tested redaction policy, preserving BCL-only contracts and transaction independence.

<a id="cl-015"></a>
## CL-015 — Complete runtime bundle, native imports and AOT inventory

**Scope/value:** PackagingOwner; M2/C; P0. Extend the early artifact to real persistence/codec use. No unsupported platform promises or publishing.

**Read/evidence:** CL-006 qualification, CL-013 manifest, selected codec/SQLite resolved dependencies, actual Unity import settings. Current package has no native dependencies.

**Dependencies/ownership:** CL-004/006/007/013; same packaging/shared files serialize with CL-013.

**Steps:** collect actual managed/transitive DLLs once, reviewed native binaries for supported Editor hosts and Android ARM64, symbols/notices/checksums and correct CPU/OS import metadata. Inventory P/Invoke names and transitive references. Provide linker preservation/generated formatter configuration for the exact used generic paths; do not simply preserve every assembly. Ship reproducible install/uninstall/upgrade instructions and prior-artifact rollback. Unknown runtime/native requirements fail packaging rather than disappearing from the manifest.

**Tests/commands:** DN/package verification plus UE/UA when runners exist. Inspect APK/IL2CPP artifacts for expected native library and architecture; execute database open/transaction/reopen and codec vectors on device through INT-009. Distinguish successful packaging, import, AOT build and actual device execution.

**Acceptance/delivery:** complete dependency/license/native matrix and reproducibility evidence. CL-015 packaging completion cannot mark INT-009 device proof passed. A missing target binary is an explicit blocked capability.

**Prompt:** Implement CL-015 to produce one complete pinned managed/native bundle, with verified dependency closure and explicit AOT/native acceptance gaps.

<a id="cl-016"></a>
## CL-016 — Reusable cross-repository fixture and fault harness

**Scope/value:** SDK test infrastructure; M3/D; P1. Make peer acceptance reproducible without inventing a working backend.

**Read/evidence:** TESTING.md, canonical fixtures, backend scripts/README/healthz versus readyz behavior and CL-003 handoff. Existing backend liveness is not feature readiness.

**Dependencies/ownership:** CL-003; own proposed tests/acceptance and fixture evidence format. No backend runtime changes.

**Steps:** add a documented executable harness accepting explicit SDK/backend commits, nonproduction endpoint and secure credential references. Provide fixture producer/consumer modes, correlated operation/stream traces, transport fault injection and database-state assertion hooks supplied by backend test tooling. Separate fake unit mode from real peer/live mode; real mode fails closed on 501, unavailable readiness, absent seed data or skipped provider verification. Persist no secrets.

**Tests/commands:** DN and the newly created, documented PEER commands. Test harness failure on unavailable backend and empty test selection; C#↔TS raw vectors; drop response after real commit; repeated operation/business source; interrupted snapshot and owner mismatch. Backend-specific seed/database commands must be supplied by the backend track, not guessed from scaffold scripts.

**Acceptance/delivery:** harness README, exact CLI help/sample nonsecret configuration, failing-unavailable test, fixture schema and evidence records containing all three revisions. No fabricated peer task IDs. Live game/device behavior is still INT-010/018.

**Prompt:** Implement CL-016 as a fail-closed peer verification harness. Distinguish scripted/fake tests from real backend execution and make the latter impossible to report green against scaffold 501 responses.
