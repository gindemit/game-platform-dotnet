# Repository audit, evidence limits and traceability

Prepared 2026-09-18 for the client planning track. This is a source audit and executable backlog, not a runtime certification.

## Exact baseline and freshness

| Repository | Default branch / inspected HEAD | Observed state |
| --- | --- | --- |
| gindemit/game-platform-dotnet | main / `f446101574e11c9885583ea184af583303b74ba1` | M0 scaffold, fifteen portable library projects, sixteen unavailable feature modules |
| gindemit/game-platform-backend | main / `66e40558045cbe5c314119022a55ced46ad9d04e` | M0 scaffold, canonical draft policy/primitive vectors, unavailable feature/API adapters |
| gindemit/MrSquareUnity | main / `a3dc3f00aea334cdf538cc34d49c2e8a92b8cbd9` | Existing playable campaign/Pocket Bloom, old pure Platform boundary, no SDK installation |

GitHub repo/default-branch/branch/PR reads were performed. No open PR was returned across the three repositories at audit time. SDK/backend showed main only. Unity also had unrelated copilot/docs branches and `work/garden-reliability-20260917` (`ca81258d63fc22132df4b77d6092824750119ae1`); these were not changed. Refresh all refs before implementation because the backend planning track and other work may advance independently.

No user local worktree was available. A container clone attempt failed with `Could not resolve host: github.com`; no checkout/build occurred and no claim of a clean user worktree is made. Repository reads used the connected GitHub tools. `dotnet`/Unity/device runners were unavailable in this session. The local checks reported in the delivery report concern newly authored planning artifacts only.

For SDK source, the initial commit `9c7ceca148a658fffaf30e6b43388184a6140f6d` was inspected, then compared to current HEAD; only IMPLEMENTATION_STATUS documentation changed. Backend initial `a1ba2b298a61998b0905f52ff771d22996e746c2` was similarly compared to current HEAD; publication/status documentation changed, not feature implementation. This supports auditing those source/test diffs against the exact current baseline without claiming a local checkout.

## Reading coverage and authority

Read the platform root/scoped guidance, README/doc indices, architecture/decisions/data-sync/features/dependencies/testing/roadmap/workflow/status/provenance, project/manifest/lock and validation/packaging/CI files, contract policy/vectors/snapshot and pin/sync tooling, M0 source and tests. Read Unity root guidance and authoritative architecture/implementation/lifecycle/DI/contract companions, with relevant sections of client storage, backend architecture, product vision and shell proposal. Inspected actual Platform contracts/runtime/fakes, outcome adapter, campaign persistence, preview controller, architecture tests, checkpoint format, asmdefs, package manifest/lock and Android entry/build workflow. Large design documents and unrelated art/difficulty research were intentionally read only where relevant. Search-discovered consumer inventory must be refreshed with an exhaustive checkout scan in INT-001; this audit does not claim a whole-project Unity compile.

For existing behavior, source/tests win over an old overview; for intended behavior, accepted decisions win over unfinished code. Backend contracts own protocol evolution; Unity accepted product/lifecycle requirements are not overridden by that ownership. Product vision is design direction; the five-destination shell remains a proposal. Source sections/paths below are relative to the named baseline repository and can be inspected using a commit-pinned GitHub blob URL or `git show <sha>:<path>`.

**ID namespaces:** A01–A13 here refer to `Docs/Architecture/PlatformRepositories/ACCEPTANCE.md`, not the older review findings A05–A11 used inside PlatformContracts. LC01–LC13 and DI01–DI12 keep their adopted meanings. A policy/Markdown validator is not a runtime acceptance test.

## Actual implementation findings

1. SDK Core currently has an ordinal string PlatformId, simple PlatformResult/PlatformFailure and a capability-unavailable exception. Typed UUID roles, v7 generation and RFC-order wire handling are not present. Existing game PlatformContracts has richer guards/enums/results/generic outcomes; naive DLL import would create parallel type families.
2. All sixteen `*Feature` classes derive from UnavailableFeature and return NotImplemented; SQLite/HTTP/codec adapters throw unavailable; navigation/presentation report unavailable. IBackendGateway/IHttpExecutor are byte-only seams, not semantic HTTP request/status/provider contracts. Sync.RunOnceAsync is unavailable. M0 tests validate these facts, not working account/game features.
3. The baseline SDK validator requires every module to remain stubbed. Its declared dependency checks are not a substitute for actual transitive assembly analysis. CL-001 must evolve status/test enforcement deliberately; deleting assertions or marking every module implemented is forbidden.
4. Runtime libraries target netstandard2.1/C#9; the build/test SDK is 9.0.203. Tests use xUnit 2.9.2, runner 2.8.2, Microsoft.NET.Test.Sdk 17.12.0 and coverlet.collector 6.0.2 in the inspected locks. These are observed pins, not recommendations to upgrade. The build SDK does not establish Unity runtime API availability.
5. Packaging currently copies internal DLLs only, not a complete transitive/native/license/AOT bundle. NativeLibrariesIncluded is false. SQLite and MessagePack selections are not installed dependencies. Optional Cysharp/DI/logging frameworks are not required by this plan.
6. Backend HTTP has liveness `/healthz`, unavailable readiness and unavailable `/v1` behavior. Health is not account/sync readiness. PostgreSQL, Supabase Auth, codec and feature adapters remain unimplemented. Its passing scaffold tests cannot establish real transactions, authentication or feed ordering. Supabase-first remains accepted; Workers/Node are separately unproven portability targets.
7. Canonical policy is `0.1.0-draft` with primitive vectors, not complete operation schemas or real peer binary fixture tests. The handoff preserves inspected hashes but requires recomputation before repinning. Logical JSON examples in Unity are not authenticated cursors or stable production APIs.
8. Unity has two relevant real consumers: `Assets/Scenes/Main.unity` with MrSquareApp/CampaignSession and `Assets/Scenes/GardenPreview.unity` with GardenPreviewController. Main is the campaign Build Settings path; the current Firebase distribution workflow deliberately builds GardenPreview through GardenPreviewBuilder.BuildAndroidForCi. Neither is a new SDK integration sample.
9. PocketBloomOutcomeAdapter reports a generic GameplayOutcome; the discovered executable caller is ArchitectureIntegrationTests with a fake sink. GardenPreviewController creates GameplayWorld/session and animates completion without a discovered runtime call to that adapter. INT-007 must wire the logical completion path, not merely add another fake test or reward animation.
10. CampaignSession uses `mrsquare.progress.v1.{catalogId}.{levelId}` and `mrsquare.difficulty.v1.{catalogId}` PlayerPrefs keys. Completion, difficulty update and save are separate operations, not an outbox transaction. Construction also saves difficulty. GardenPreferences stores six installation feedback booleans under `PocketBloom.Feedback.v1`. Sandbox isolation must cover constructor/reset paths, not just a final result callback.
11. GameplayCheckpoint is game-owned `MGD1:` base64 with a binary MGD1 header, domain/rules/config versions, seed/tick/revision/event sequence, mechanics snapshot, cells and ambient extension; signature uses SHA256 over serialized text. Network MessagePack adoption is not permission to rewrite this format. The preview's in-memory checkpoint support is not proof of durable resume.
12. Unity is pinned to 6000.5.3f1. Android bootstrap selects ARM64, IL2CPP, standard Activity, API26 minimum. Serialized API compatibility and managed-stripping maps are empty, so effective settings must be logged from that editor rather than guessed. Manifest requests test framework 1.6.0 and UGUI 1.0.0; inspected lock resolves 1.7.0 and 2.5.0. INT-001 records actual resolution and does not silently upgrade. rlottie is pinned independently; its native success proves nothing about SQLite.
13. `.github/workflows/firebase-app-distribution-android.yml` runs on every push to main and distributes an APK. This planning track uses an unmerged documentation PR; do not merge/dispatch it automatically or describe Firebase distribution as the game backend.

## Reference/ownership reconciliation

The SDK actual graph is narrower than parts of the target Unity map: Backend.Contracts and Storage.Abstractions currently reference Core only; target responsibilities allow selected feature-contract edges. SDK codec assemblies currently reference Core as well as Wire/Transport, whereas the target map lists a narrower codec edge set. Diagnostics allowances differ in some outer assemblies. These are explicit CL-001 review items, not permission to broaden every reference or move all concrete implementations into infrastructure.

Initial artifact route: one versioned managed DLL/native bundle, evolving the existing packaging script. Early core-only import is CL-013/INT-002; full codec/SQLite dependency closure is CL-015/INT-009. UPM may later wrap that same artifact, but the same runtime library must not be acquired twice. Exact SQLite source/native pin remains CL-006 qualification, not a fabricated installed version.

The old phase-E wording requiring a second game before extraction is reconciled as follows: repository scaffolds already exist, but extraction/adoption is not complete. A controlled early first-consumer import is needed to test the architecture; no stable reusable package/API promotion or final bridge removal is claimed until INT-016/017. A directory or fake second game is not reuse proof. Existing phases A–E and M0–M5 remain planning landmarks, with native/packaging work brought forward and one minimal value path allowed for the early live slice.

## Requirement-to-evidence-to-task matrix

| Invariant / accepted source | Current source evidence | Missing work / status | Tasks | Acceptance evidence |
| --- | --- | --- | --- | --- |
| Portable dependency boundaries; AssemblyResponsibilities/assembly-map | Fifteen SDK projects and current Unity Platform asmdef; target/actual graph differences | Planned reconciliation and controlled extraction | CL-001/002, INT-001/002/017 | Actual assembly graph/API diff, DN+Unity compile, DI09/A10 |
| Exact IDs/numbers; PlatformContracts §1 | String PlatformId; primitive JSON vectors only | Stubbed codec/generator semantics | CL-002/003/004 | RFC bin16, int64/compact/unsafe-number/timestamp vectors; A01/A02 |
| Durable command identity; PlatformContracts §2; DataAndRuntimeFlows §2 | UnavailableSqliteStore; PlayerPrefs separate saves | Planned real atomic transaction | CL-007/008/009/108, INT-005/007 | Crash/disk/duplicate/clone/gap SQL assertions; A03 |
| Private committed feed; PlatformContracts §3 | Backend/SDK sync unavailable | Blocked canonical operation schema/live server plus local implementation | CL-003/009/010/016, INT-010 | Fixed watermark, full groups, no ACK cursor, PostgreSQL peer proof; A04/A05 |
| First online complete bootstrap; PlatformContracts §4; storage §8 | No SDK account provisioning in either real scene | Planned state machine and wiring | CL-101/010, INT-004/006/010 | Interrupted all-page bootstrap, later offline boot, unchanged identity; A05/LC08 |
| Trusted atomic value; PlatformContracts §7 | Sixteen unavailable services and backend adapters | Blocked real server transaction/receipt proof | CL-104–107/111/112, INT-010/011/013 | Operation+business-source uniqueness, no partial debit/grant/feed; A06 |
| Scope/caches; PlatformContracts §§3.6,6 | No SDK scoped cache/projection; legacy in-memory tests only | Planned scoped durable/query state | CL-011 and all features, INT-018/019 | Two apps/accounts/streams/visibility changes/late callbacks; A07 |
| Typed objectives/extensions/audit; PlatformContracts §5 | Legacy single QuestCounter is not general objectives | Planned shared engine and bounded versioned payloads | CL-109/110 plus CL-011, INT-012 | Multi-objective, duplicate/upgrade/forged-actor/cycle fixtures; A08 |
| Native target support; dependency policy | No SQLite/MessagePack installation; existing rlottie unrelated | Unverified Editor/device/AOT compatibility | CL-006/013/015, INT-002/009 | Native binary inventory, import, IL2CPP, real Android SQL/network; A09 |
| Host/manual DI; lifecycle and injection decisions | Legacy synchronous PlatformHost and caller-owned providers; no accepted async host graph | Planned ownership/lifecycle implementation | INT-003/004/006/019, CL-012/014 | LC01–13 and DI01–09/12 runtime tests; A10 |
| Future container-specific guarantees; DI10/11 | No selected container | Conditional future gate, not passed or silently waived | INT-019 records applicability; new adoption task required before installing container | DI10/11 tests on pinned container only if adopted; manual constructor/IL2CPP evidence remains mandatory |
| Host portability; backend architecture | Supabase/Workers/Node shapes, not proven deployments | Backend-owned, separately blocked | CL-016/INT-010 link peer evidence | A11 reported separately per host, never inferred from Node mocks |
| Auth/verification/security; backend identity and purchase requirements | Unavailable provider adapters, no live verification | Blocked real trusted provider/secret-store proof | CL-005/101/112, INT-004/013/018 | Invalid token/issuer/audience/expiry, receipt forgery, redaction; A12 |
| Migration/release evidence; TESTING, storage §9 | M0 package script, existing PlayerPrefs/checkpoint | Planned upgrade/rollback/compatibility | CL-007/013/015, INT-005/017/020 | Fresh/upgrade/crash/restore and immutable compatible artifact record; A13 |
| Real game outcome; actual preview/campaign controllers | Existing playable levels; adapter exercised by fake EditMode sink | No durable runtime SDK call | CL-108, INT-006/007/008/010 | Real scene → offline completion → restart → backend receipt → confirmed UI |

## Sixteen-feature traceability and UI separation

Each row inherits its explicit local/server/cache/port/persistence/test policy from its full card and the corresponding `docs/FEATURES.md` module row. All SDK statuses at baseline are **stubbed**; SDK installation/adoption in MrSquare is **not_installed**, not inferred from old similarly named types.

| Module | SDK task | Real consumer task(s) | Additional live/device gate |
| --- | --- | --- | --- |
| Accounts | CL-101 | INT-006 | INT-010/018 |
| Profiles | CL-102 | INT-008 | INT-010/018 |
| Catalog | CL-103 | INT-011 | INT-018 |
| Inventory | CL-104 | INT-011 | INT-018 |
| Wallet | CL-105 | INT-010/011 | INT-018 |
| Entitlements | CL-106 | INT-011 | INT-018 |
| RewardFulfillment | CL-107 | INT-010/011 | INT-018 |
| Progression | CL-108 | INT-007 | INT-010/018 |
| Quests | CL-109 | INT-012 | INT-018 |
| Achievements | CL-110 | INT-012 | INT-018 |
| Store | CL-111 | INT-013 | INT-018 |
| Purchases | CL-112 | INT-013 | Store sandbox/device/backend verification |
| Leaderboards | CL-113 | INT-014 | INT-018 |
| Teams | CL-114 | INT-014 | INT-018 |
| RemoteConfig | CL-115 | INT-015 | INT-018 |
| Inbox | CL-116 | INT-015 | INT-018 |

## Lifecycle and injection test mapping

INT-003 owns pure transition/ownership tests for LC01–04 and DI01/02/04/08. INT-004/006 add platform threading, account/publication guards and readiness (LC05–08, DI05/06). INT-008 supplies reset/navigation, screen borrowing and pooled binding (LC09, DI02/03/06/07). INT-009 covers native target disposal (LC13/A09). INT-019 reruns LC01–13 and DI01–09/12 through actual PlayMode/device scenarios, including reload combinations, sandbox isolation and late callback races. DI10/11 stay explicitly conditional on future container adoption; they are not green because no container is installed. No Task allocation/resource budget is asserted without measured hardware/build evidence.

## Unresolved choices and safe recommendations

Legacy account-free progress cannot automatically become server-trusted reward eligibility. DEC-LEGACY-TRUST recommends preserving it locally as an unverified one-time import after explicit ownership selection; backend/product owner must approve authority treatment. DEC-CONTENT-VALIDATION requires a nonproduction game-validation adapter and reward source policy; a client-generated signature is not trusted anti-cheat proof. Missing operation schemas, provider/runner credentials, native qualification and actual live API are blockers recorded independently so pure tests/lifecycle/navigation/inventory work can continue.

This plan does not change production gameplay, animation content, difficulty rules, checkpoint encoding, existing issued IDs, deployment hosts or repository visibility. It does not schedule Codex or claim subagents have been launched.
