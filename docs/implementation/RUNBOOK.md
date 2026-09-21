# Codex launch, coordination and verification runbook

This is an implementation **plan**, not a record of completed features. Read README/AUDIT and the selected card; keep task context bounded. Both repositories' documentation branches must be reviewed/pinned for a launch. Do not automatically merge Unity main, which triggers Android distribution.

## 1. Operator preparation and first safe launch

Use the existing repositories, never replacements. In each checkout run `git status --short`, `git branch --show-current`, `git rev-parse HEAD`, and `git remote -v`; fetch without resetting local changes. Record source and reviewed plan commits. Use a new worktree for each task, based on the reviewed integration branch—not another worker's unreviewed branch. Example after choosing the actual base:

```sh
git worktree add ../gp-cl-001 -b impl/CL-001-interface-baseline <reviewed-base-commit>
python docs/implementation/validate_manifest.py --ready
python -m unittest discover -s docs/implementation -p 'test_*.py'
```

`<reviewed-base-commit>` is a required operator-resolved revision, not a literal command argument or instruction to use stale baseline main. Check `git worktree list` and existing branch names first; never reset/abandon an existing task. Each task's root and applicable scoped AGENTS apply. Credentials stay in authorized local/CI secret storage; no `.env`, token, keystore or receipt contents in PRs/reports.

Assign responsibilities: PlatformCoordinator integrates SDK interfaces/graph/status; ClientContractOwner reviews mirrors/DTOs; BackendContractOwner alone changes canonical protocol; StorageOwner integrates transaction/migration registry; PackagingOwner owns resolved dependency/bundle files; ConsumerIntegrationOwner alone integrates shared Unity files; an independent reviewer/verifier inspects evidence. These are roles for the operator to assign, not claims that agents have launched.

### Ready-to-start table

| Task | Independent useful work | Completion limits |
| --- | --- | --- |
| CL-001 | Actual graph/interface/status-validator review | Must preserve negative architecture/evidence tests |
| CL-006 | SQLite source/license/native acquisition qualification | Missing exact binary/runner stays explicit; device execution is a separate gate |
| CL-014 | Pure diagnostics/redaction tests | No new logging framework by default |
| INT-001 | Exhaustive caller/import/save/settings inventory | Actual Unity tests require the licensed editor; static inspection is not compilation |

These four disjoint scopes are the initial useful concurrency limit, not a demand for four agents. Use fewer when tool/runners/context are scarce. A separate backend owner can prepare PEER-CORE without touching client files. After interfaces freeze, independent feature subdirectories may run concurrently; shared project/contract/migration registration and Unity integration remain single-owner bottlenecks. Adding agents to a serialized surface does not add useful parallelism.

## 2. Dependency DAG and waves

The machine-readable DAG is every task's `dependencies` in execution-manifest.json. Its conservative wave numbers additionally serialize overlapping owned paths/locks. They are **review/scheduling bands, not dates, agent counts or mandatory global barriers**. A blocked backend branch does not prevent independent lifecycle/navigation/package work. Start a task only when its actual predecessors and peer contracts have evidence and its lock is free. Revalidate after any rescheduling.

| Wave | Tasks |
| --- | --- |
| 0 | CL-001, CL-006, CL-014, INT-001 |
| 1 | CL-002, INT-003 |
| 2 | CL-003, CL-012, CL-013 |
| 3 | CL-004, CL-005, CL-016, INT-002 |
| 4 | CL-007, INT-004 |
| 5 | CL-008, CL-015 |
| 6 | CL-009, CL-011, INT-005 |
| 7 | CL-010, CL-102, CL-103, CL-114, CL-115 |
| 8 | CL-101, CL-104, CL-105, CL-106 |
| 9 | CL-107, CL-108, INT-006 |
| 10 | CL-109, CL-111, CL-113, CL-116, INT-007 |
| 11 | CL-110, CL-112, INT-009 |
| 12 | INT-008 |
| 13 | INT-010, INT-011 |
| 14 | INT-014, INT-016 |
| 15 | INT-015 |
| 16 | INT-019 |
| 17 | INT-012 |
| 18 | INT-013 |
| 19 | INT-017, INT-018 |
| 20 | INT-020 |

Prioritize the first real slice over ready breadth work: the wave table is valid but not an assertion that every lower-numbered unrelated feature must finish first. INT-010 does **not** depend on Quests, Achievements, Store, Purchases, Teams, Leaderboards, RemoteConfig or Inbox completion. INT-008 includes the minimal one-currency display for the optional bounded server-issued test reward; its gameplay completion remains unvalidated. INT-011 later expands value UI. Native packaging starts early because late AOT discovery can invalidate desktop-only assumptions.

Critical edges in plain text:

```text
CL-001 + INT-001 -> CL-002 -> CL-013 -> INT-002       first real SDK import
INT-001 -> INT-003 -> INT-004/006 -> INT-019         host/runtime proof
PEER-CORE -> CL-003 -> CL-004/005                   frozen wire/provider seam
CL-006 + CL-002 -> CL-007 -> CL-008                 real crash-safe storage
CL-008 + codecs/providers -> CL-009/010/011          push, pull, snapshots
CL-010/011/005 -> CL-101 -> INT-006                  complete account bootstrap
CL-108 + INT-006 -> INT-007 -> INT-008               real pending progress/UI
CL-015 + INT-006 -> INT-009                         IL2CPP/native device proof
CL-104/105/106 -> CL-107 + real backend -> INT-010    first live reward slice
INT-010 -> INT-016 -> INT-017                       mature reuse/bridge removal
breadth UI + INT-019 -> INT-018 -> INT-020           final evidence/compatibility
```

M0 remains historical scaffold work. M1 contracts/import; M2 durability/provisioning/host; M3 progression/sync plus early real consumer; a deliberately minimal M4 value path enables INT-010; remaining M4 objectives/store and M5 breadth/reuse follow. Unity A–E are retained, not replaced: A boundary, B composition/presentation, C persistence, D real transport, E breadth/reuse. Early import is not permission to claim the former second-game-before-stable-extraction gate passed.

## 3. Explicit contract, packaging and lifecycle gates

**Contract gate (G1 schema only):** PEER-CORE and CL-003 must supply complete operation/error/snapshot/fingerprint schemas, semantic/negative vectors, field mappings, reviewed canonical and mirror commits/digests, actual reviewers and compatibility policy. Runtime revisions may remain null/unverified. Peer binary fixtures and executable bidirectional codec evidence belong to G2, not this prerequisite; A02 remains unpassed until that exchange. Primitive vectors and Unity logical examples are insufficient. Feature families additionally require PEER-VALUE/OBJECTIVES/COMMERCE/SOCIAL/CONFIG-INBOX. Unknown required change kinds block checkpoint advancement. Only the canonical owner changes backend files; post-freeze deltas require both reviewers, fixtures and coordinated repins.

**Packaging gate:** CL-006 records the actual SQLite source/native pin and license/distribution; CL-013 supplies early managed import; CL-015 supplies dependency closure/native/AOT metadata. One bundle acquisition mechanism per runtime library. Preserve netstandard2.1/C#9 and build SDK unless a reviewed compatibility change explicitly approves otherwise. Actual Unity resolved package versions and effective settings are recorded in INT-001, not guessed from a build SDK or an empty serialized map.

**Lifecycle gate:** no new account application while old writer/sender has not quiesced. Cancellation is not evidence of stopped work. Keep caller cancellation separate from shared transitions and durable operations. Screen closure only disposes owned view/presenter/subscription/lease objects. No global Current/service locator, universal services bag or mandatory DI framework. DI10/11 are conditional future-container requirements; manual DI and AOT tests remain required now.

**Trust gates:** DEC-CONTENT-VALIDATION is accepted for the bounded initial unvalidated tier: support both client-side and server-side rewards, use client-side rewards initially, and let the optional G3 server fixture accept the authorized client completion report under explicit client trust before issuing one nonpremium `test.coin` through normal Wallet/receipt/idempotency controls. This is not gameplay/session validation. Preserve a future game-owned validator boundary; do not require TypeScript replay or the existing .NET game DLL now. The legacy-save decision is also resolved: there are no production users to migrate, so old gameplay PlayerPrefs/checkpoints are never imported, merged or rewarded. Fresh platform/game-owned SQLite state is the only supported account-owned path.

## 4. Exact commands and environment labels

### DN — existing SDK baseline commands

From game-platform-dotnet root with Python 3.10+ and the repository-pinned .NET SDK (global.json: 9.0.203/latestPatch at audit):

```sh
python scripts/validate.py
python -m unittest discover -s scripts -p 'test_*.py'
dotnet restore GamePlatform.sln --locked-mode
dotnet build GamePlatform.sln -c Release --no-restore
dotnet test GamePlatform.sln -c Release --no-build --no-restore
python scripts/package-sdk.py
```

These commands were verified by reading the existing scripts/toolchain; **not executed against a local repository checkout in this planning session**. New implementation must update relevant scaffold-only assertions in the same PR as real behavior tests, not disable tests. Archive exact exit codes, test counts and TRX/output. A filter returning zero tests is failure for acceptance, even if the runner exit code is zero.

### Planning validation — tooling created by this documentation change

```sh
python docs/implementation/validate_manifest.py --ready
python -m unittest discover -s docs/implementation -p 'test_*.py'
```

This checks manifest structure, duplicate/missing IDs, cycles, dependency waves, same-wave path/lock conflicts, feature ledgers and unsupported completion claims. It cannot prove the truth of runtime evidence. Card-path content review and cross-repository link checking remain separate review duties.

### SQ — proposed real SQLite tests, created by CL-007/008

Use DN plus the real-driver tests marked with xUnit trait `Category=Sqlite` (the trait/test set must first be introduced):

```sh
dotnet test GamePlatform.sln -c Release --no-build --no-restore --filter 'Category=Sqlite' --logger 'trx;LogFileName=sqlite.trx'
```

Require a nonzero executed-test count and archive the selected native library/version plus process-kill fault artifacts. An in-memory database mock is not SQ. Desktop SQ is not Android native acceptance. No test invokes production databases or deletes the only copy of player progress.

### PEER — explicitly blocked until CL-016 and backend handoff

CL-016 must add/document a fail-closed CLI under `tests/acceptance/` with fixture produce/consume and real API modes. The exact project path/CLI is **not present in the scaffold**, so do not invent a command claiming it exists. The backend track must provide its actual seed/start/fixture/export commands and compatible commit. Add those exact commands to the evidence/runbook once implemented. A `/healthz` 200 with unavailable readiness or `/v1` 501 is a failed prerequisite, not a passing peer integration.

Require C# encode→actual TS decode and reverse, canonical-semantic fingerprint equality, real PostgreSQL transaction/feed/receipt assertions and exact schema/fixture versions. Distinguish Supabase, Workers and Node host acceptance individually; client work does not certify all hosts from one test server.

### UE / UP / UA — Unity and device commands

Use the exact existing Unity EditMode/PlayMode and Android build entry points plus the **proposed isolated test build** documented in the paired [LIVE_SLICE.md](https://github.com/gindemit/MrSquareUnity/blob/docs/client-sdk-integration-plan-2026-09-18/Docs/Architecture/ClientImplementation/LIVE_SLICE.md). Close the editor before batch tests. Do not append `-quit` to `-runTests`. Native build, physical-device execution and real-backend behavior are separate evidence rows. The normal Android builder's application identifier must not overwrite a real installed user's data; INT-009 creates the isolated test variant before install/run automation.

## 5. Evidence and status updates

Each evidence record must contain task ID, source/plan commits, exact SDK artifact hashes, backend/schema/fixture revisions, consumer commit, environment/tool/device versions, command, exit code, nonzero test count where relevant, raw report path, expected/actual result, fake versus real provider, and unrun/blocked gates. Do not commit credentials, raw purchase receipts or private user data. Store test-only scoped IDs and redacted correlation instead.

Task states: planned → in_progress → complete, or blocked with reason. `complete` requires evidence and review; it does not automatically update feature integration. Feature sdk_status starts stubbed; mrsquare_integration_status starts not_installed. `implemented` requires the completed library task and runtime behavior evidence. `wired` may record intermediate consumer code; `integrated` requires separate actual consumer, real-backend and applicable device evidence plus a compatible artifact/schema pair. Unavailable external runners stay unverified/blocked, never green.

At every merge boundary the coordinator refreshes HEAD/dirty state, reviews the diff against the allowed file list, reruns required tests, checks interface/pin delta and downstream compatibility, integrates migration/manifest registration, then updates the ledger once. Never auto-merge unrelated work or use force pushes. Maintain a narrow PR per task/owned surface; split a task into documented sub-PRs when necessary without inventing completion.

## 6. Copy-pastable orchestrator prompt

```text
Act as the implementation coordinator for gindemit/game-platform-dotnet and its real gindemit/MrSquareUnity consumer. Read root/scoped AGENTS.md, docs/implementation/README.md, RUNBOOK.md, AUDIT.md and execution-manifest.json, then only task-specific mandatory context. Refresh all three repository HEADs and local worktree state; preserve unrelated changes. Record reviewed planning commits.

Assign the named contract, storage, packaging, consumer-integration and independent-review responsibilities. Spawn bounded subagents only when your tools support it; otherwise execute serially and report that. Give each worker one ready task, an isolated worktree/branch, exact owned paths, frozen interfaces and evidence expectations. Start with CL-001, CL-006, CL-014 and INT-001; do not exceed the available disjoint scopes/runners. Never run multiple agents against the same mutable worktree or shared Unity/project/contract/migration surface.

Prioritize CL-013/INT-002 early SDK import, then complete first account/bootstrap, atomic real level persistence, restart/reconnect and INT-010 real-backend/device reward evidence before broad feature polish. Do not wait for all sixteen features to adopt the SDK. Continue independent work when a peer gate is blocked, but do not fabricate endpoint/fixture/BE-task completion or fake successful capabilities.

Keep accepted UUID/int64/MessagePack, immutable outbox, private-feed, server value authority, first-online bootstrap, manual DI and host-quiescence rules. Request canonical protocol changes through BackendContractOwner and repin only after review. Verify actual tests, references and real Unity callers before merging each narrow PR. Update separate SDK and MrSquare ledgers with exact evidence. No publishing, deployment, production migration, secret change, real-money purchase, force push or automatic Unity main merge is authorized. Finish each session with completed task IDs, commits, tests, blockers, compatible revisions and the next genuinely ready task.
```

## 7. Reusable worker prompt

```text
Implement task <TASK-ID> from the reviewed execution-manifest and its exact task-card anchor. Read the common delivery contract plus the card's minimal sources, root/scoped AGENTS and frozen interfaces. Before editing, report baseline HEAD/dirty files, real current APIs, allowed files, shared locks, predecessor evidence and external prerequisites.

Implement the ordered behavior and failure paths with real tests; add no game/provider/native/serializer dependencies to forbidden layers. Do not improvise canonical schemas, fake successful feature results or broaden scope. Request shared-file/contract edits through the owner. Preserve command/account identity, old saves and unrelated work. Use a separate branch/worktree and no force push.

Deliver a reviewable PR, exact changed files, commands/exit codes/test counts/raw evidence, compatibility/rollback notes, remaining unsupported cases and a coordinator-applied status/docs patch. Mark blocked/unrun prerequisites honestly. SDK tests alone do not complete Unity integration; do not update a feature to integrated yourself.
```

## 8. Independent reviewer prompt

```text
Review <TASK-ID>/<PR> against its card, manifest ownership and authoritative acceptance IDs. Read the actual diff and tests; do not rely on the worker summary. Check canonical type ownership, real dependency closure, version pins, cancellation/quiescence, captured account generation, transaction boundaries, immutable outbox identity, no ACK cursor advancement, exact numeric/codec handling, durable versus cached state, server authority, and preserved saves/checkpoints/visual controls.

Reproduce relevant commands in an independent environment, reject zero-test or fake-backend evidence labeled real, and verify all required artifacts/revisions. Check the SDK and consumer statuses separately. Identify findings by severity and file/line, propose bounded corrections and record approve/request-changes with remaining blocked gates. Do not deploy, publish, weaken tests or auto-merge unrelated changes.
```

## 9. Integration-verification prompt

```text
Execute INT-010 (or INT-018) using the paired Unity LIVE_SLICE runbook and exact reviewed SDK/backend/consumer revisions. Verify prerequisites and test isolation first. Use the actual GardenPreview/Main consumer, real native SQLite, real compatible nonproduction backend and authorized Android test device. Do not substitute a new sample scene, fake provider or liveness endpoint.

Prove online full first bootstrap, offline real level, atomic local projection/outbox, kill/reopen, same pending identity, reconnect, and either the initial client-side reward or the optional bounded backend `test.coin` reward/receipt, followed by ordered pull and truthful actual UI. When exercising server issuance, label the completion accepted-under-client-trust/unvalidated, never technically verified. Add lost-response/duplicate/rejection/account-switch/migration/interrupted-bootstrap cases. Inspect server and local durable state, not just screenshots. Report every environment separately with raw evidence and pass/fail/blocked; missing credentials/runners/APIs leave the gate blocked. Make no production or publication changes.
```
