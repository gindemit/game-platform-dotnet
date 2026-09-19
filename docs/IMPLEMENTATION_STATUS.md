# Implementation status

## CL-009 ordered sender increment — 2026-09-19

Source commit `6ee33493e2919086331918210d01aa26835243ac` implements
the dependency-ready local sender foundation over a narrow remote command port.
Real SQLite transactions lease only the next contiguous sequence, exclude a
competing sender, retain uncertain deliveries in-flight until lease expiry, and
atomically persist accepted or terminally rejected results with
`finalizedThrough`. Known non-delivery/retryable failures release only the same
immutable operation. No push acknowledgement can touch a pull cursor.

At the clean source commit, focused CL-009 tests passed 5/5 and the full SDK
suite passed 276/276. Locked restore/build covered 18 projects with zero
warnings/errors; architecture validation, 33 script tests and the 52-task/
16-ledger/21-wave manifest validation passed. CL-009 remains `in_progress`:
the frozen push/receipt HTTP provider, bounded batching/backoff, blocked-auth
state, real backend/peer fault execution, overlay rejection rebuild,
restart/process-kill/device/Unity evidence and independent A03/A07/A12 review
remain open. G3 was not assessed and P4 was not started.

## CL-012 minimal navigation complete — 2026-09-19

Source commit `010d47c07907baf86d8a1c9396f81cd5628d03d2` completes
the pure SDK task with 7/7 focused and 271/271 full tests passing, plus clean
build/planning validation. See
[CL-012 evidence](implementation/evidence/CL-012/README.md).

This does not claim INT-008: no Unity scene, game binding, visual/control
PlayMode or device execution occurred. Those consumer gates remain outstanding,
as do G3 and P4.

## CL-005 semantic provisioning HTTP — 2026-09-19

Source commit `5b7308d5537462623ef7cf6b3b53bcd1312439bf` implements
the portable provisioning provider over injected neutral executor/codec/auth
ports. Focused tests pass 12/12 and the full suite passes 264/264 with zero build
warnings/errors. See [CL-005 evidence](implementation/evidence/CL-005/README.md).

CL-005 remains `in_progress`: the other frozen operations, production codec and
host registration, BE-016 composition, Unity executor and live/device evidence
remain unavailable. The provider does not make incomplete BE-008 provisioning
truthful and does not enable a route, feature, G3 or P4.

## CL-008 atomic projection/sequence/outbox — 2026-09-19

Source commit `4de294141ea7d0f6cf062b8ce736ea0ce89fa112` implements
the qualified Windows x64 atomic admission slice. Eight real-native cases pass,
including actual pre/post-commit process termination and reopen; the full suite
passes 252/252 with zero build warnings/errors. See
[CL-008 evidence](implementation/evidence/CL-008/README.md).

CL-008 remains `in_progress`: disk-full/corruption, terminal result persistence,
authorized server stream rotation, Unity/AOT/device and real game projection
integration remain. No stream is silently reset or relabeled. CL-009/010,
CL-015, INT-005/007/009, G3 and P4 remain outstanding.

## CL-007 real SQLite executor — 2026-09-19

Source commit `91a67c7805ff59bc3b0ca286fddc2a5124291188` implements
the qualified Windows x64 portion of CL-007: one serialized native connection,
scope-owned migrations with checksummed journal markers, callback-borrowed
transactions, cancellation and bounded quiescent disposal, and typed storage
failures. The real-native CL-007 suite passes 8/8 and the full SDK suite passes
244/244; locked restore/build, 33 script tests, manifest validation and 14
manifest tests also pass with zero build warnings/errors. See
[CL-007 evidence](implementation/evidence/CL-007/README.md).

CL-007 remains `in_progress`, not production-accepted: abrupt process-kill,
disk-full/corruption, other native targets, Unity IL2CPP/AOT/stripping and device
tests remain unrun. CL-015 and INT-009 therefore remain outstanding, SQLite is
not yet composed into the consumer, and G3/P4 are not claimed. CL-008 is now
dependency-ready for bounded implementation.

## P3 dependency-ready SDK preflight — 2026-09-19

The coordinated branch remains at `06ba050929117bd48cba5993c28156bbb2c6418c`.
Fresh locked restore and Release build passed with 18 projects and zero
warnings/errors; the full SDK suite passed 236/236. The pinned CL-006 SQLite
candidate also passed its real Windows x64 rollback/commit/reopen probe against
SQLite 3.50.1, preserving signed-64 value `9007199254740993`.

An initial invocation with Windows PowerShell 5 failed the runner's environment
guard because that shell does not define `$IsWindows`; the PowerShell 7
invocation passed. This failed attempt is retained and is not an SQLite test
failure. CL-005 and CL-007 predecessor evidence is present; their manifest
`Await predecessor evidence` reasons are stale scheduling metadata. Both tasks
remain planned: no HTTP or durable SQLite implementation, package, Unity import,
native/device acceptance, feature capability, G3 result or P4 work is claimed.

## G2 verified desktop core - 2026-09-19

G2 post-promotion core exchange passed: 34 cases/24 fingerprints each direction, 32 raw probes in both decoders and 39 diagnostic negatives. Backend 229 tests; SDK 236 tests. Additive receipt schema approved separately with 30 cases/six known answers; its runtime remains unverified. All service features, production/native/device, G3/G4 and product trust/reward/legacy approval remain unavailable or unverified. See [G2 evidence](implementation/evidence/G2-epoch3/README.md). Central state is published last and alone releases P3.

## P2 epoch 2 client delivery — 2026-09-19

G1 is passed and P2 authorized by backend `19aef14eaa6ca428775ec7ff7a4f69cfeab0e20a`.
CL-002 canonical/compatible core, CL-003 DTO delivery and CL-013 early managed
bundle are implemented. CL-004/016 provide a qualification codec, typed mappings,
fingerprints and real producer/consumer CLI; production codec and live-service
modes remain unavailable. See [exact P2 evidence](implementation/evidence/P2/README.md).

Locked Release build and 222 .NET tests pass, alongside 73 schema cases, 32
fingerprint vectors, five hostile JSON cases, six CLI failure/conversion tests and the
architecture/manifest/package checks. An exact Core/Features bundle is imported
into the real consumer at `7375251e5302db308198b7956037c1b33e6edfb6`: Unity
6000.5.3f1 selected EditMode 7/7, Platform 14/14 and GameplayDomain 27/27 pass.
INT-002 remains partial for visual/control PlayMode and tested rollback; the
historical unavailable-editor blocker is superseded, not evidence of device/AOT
acceptance. Existing 123 documentation errors remain unwaived.

34 C# MessagePack fixtures and 24 fingerprints are committed for G2. Actual
TypeScript exchange and minimal live-slice schema review remain unrun. STOPPED AT
G2; P3 is not released. Full A01–A13 production, native/device/provider/live-game
gates and unresolved product policies remain pending. All sixteen product feature
ledgers remain stubbed/not_installed. Changes are published on coordinated
execution/task branches, not main, per CODEX_CLIENT.md.

## G1 epoch 2 schema review — 2026-09-19

G1-R1–R5 are resolved at schema level against canonical
`0109936f0ebf232924cec70adb79c4f790b604bc`, version `0.2.0-core-schema.1`.
The exact reviewed 90-file mirror is installed, with all original policy/primitive
bytes preserved. Client review validates 73 schema cases and 16 independent
boundary cases and supplies 290 property/branch mappings. Review tooling now
enforces new token/stream/distinctness/depth constraints; no DTO or codec is
implemented by this work. See [review evidence](implementation/evidence/CL-003/G1-epoch2/README.md).

CL-003 remains partial for attribute-free DTO delivery during P2; schema/mirror
review is complete. Other tasks/features retain their actual status. Central
backend state releases P2 only after cross-repository publication. Runtime
C#/TS compatibility, A01–A13, native/device, product trust/legacy/reward decisions
and the existing Unity documentation path failures remain unverified/unresolved.
Historical candidate.1 observations below are preserved as dated evidence.

## P1 epoch 1 client remediation — 2026-09-19

CL-003 client review inputs now include 259 field/branch C#/TS mappings, all ten
existing GameplayOutcome fields, and explicit G1-R1–R5 consumer-preservation and
schema acceptance expectations. Read-only review tooling verifies immutable Git
objects and hashes without copying the unapproved mirror. Candidate.1 passes
49 diagnostic schema fixtures and 16 independent client boundary cases; ten
review-tool regression tests pass. These results do not resolve its semantic
gaps. See [epoch 1 review](implementation/evidence/CL-003/epoch1/README.md).

The fetched backend execution SHA remains
`c6aa77054ec63c0ae46fe2113d3add994bf382fb` (P1/epoch 1/blocked), still publishing
candidate.1. Review against the corrected immutable candidate is blocked until
it is published. CL-003 remains blocked; no G1 approval, mirror repin, DTO or P2
codec work occurred. The unchanged consumer SHA is
`bc53723a8eec3521dd7bd8e05eaa726fca29795d`. No Unity write was needed.

Exact command/exit/test evidence is recorded in the epoch-1 verification report.
Architecture/mirror validation, 19 validator tests, manifest validation and 14
manifest tests pass. No runtime source changed; .NET/native/Unity/device/live
backend suites were not rerun. A01–A13, G1–G4, product-policy and existing
consumer environment blockers remain unpassed/unresolved.

## G1 synchronization review — 2026-09-19

SDK remains P1; G1 is blocked on candidate/consumer mapping, stream and
receipt recovery semantics, full fingerprint known-answer vectors and decoded
allocation/compression policy (G1-R1–R5). No runtime behavior, canonical/mirror
pins or product authority changed. BE-002/CL-003 remain partial/blocked; P2 is
not released. A01–A13 and G2–G4 remain unpassed. The coordinator reviewed all
three immutable input heads; original Unity settings edits are preserved.

Review and next work: [G1 client findings](implementation/evidence/CL-003/G1_REVIEW.md).
SDK planning validation and 14 manifest tests passed; architecture validation,
19 validator tests and existing mirror pins passed. Raw results are preserved
in the backend G1 evidence. Older CL-003/runbook codec prerequisites were
corrected to schema-only G1 acceptance; no runtime suite was needed for these
documentation edits. Unity/editor/device and runtime peer exchange were not run.

## Coordinated P1 evidence — 2026-09-19

Run `platform-2026-09-19`, P1 / epoch 0; stop gate G1. Work is on `impl/platform-coordinated-2026-09-19`, not main. The backend state observed at published commit `d65301d39000e6817ca2b737bf9c9e49af0ed3d3` is parallel P1 with every gate pending and no approved artifacts. See the [task ledger](implementation/execution-manifest.json), [client handoff](implementation/coordination/CLIENT_HANDOFF.json), [review record](implementation/evidence/P1/REVIEW.md) and [raw SDK verification](implementation/evidence/P1/results.json).

- CL-001 complete as design/policy: exact current API/direct/transitive dependency inventory, reviewed schema-neutral target signatures and ownership/error rules, narrow feature-contract reference allowances, deny-by-default packages and evidence-aware validator. It rejects known unavailable feature relabels, missing/escaped reports, zero/mismatched tests, package/reference leakage and contract drift. Target transaction/snapshot/command types are explicitly future implementations; operation body/wire details require G1. This is tooling/interface evidence, not feature implementation.
- CL-006: pinned unity-sqlite-net 1.3.2 and native inputs, single SDK-bundle acquisition decision, real Windows x64 rollback/commit/reopen probe with exact signed-64 value `9007199254740993`. Independent rerun passed. Existing databases and dirty acquisition trees are refused. SQLite remains absent from production SDK/Unity imports; other platforms, AOT and device acceptance remain unrun.
- CL-014: implemented BCL-only safe diagnostics with enabled/deferred fields, stable code-defined event/category symbols, explicit scalar metric allowlist, default string/object redaction, immutable captured correlation/generation, bounded copied fields and failure containment. Preserves legacy IAppLog/NullAppLog. Independent review and 12 selected tests pass. No framework, queue or Unity output provider added.
- CL-003 preparation: G1 operation/schema/fingerprint/negative-vector requirements and candidate absence findings committed. No canonical contract, mirror hash, DTO or codec implementation changed. Actual schema approval and runtime compatibility remain unverified.
- INT-001: MrSquare exhaustive caller/save/import/asmdef/GUID/source-linked-project inventory and baseline regression evidence committed on its execution branch while preserving newer main work and original local edits. Inventory subpart is complete; the whole task remains blocked on the pinned Unity editor/effective settings and fresh UE/UP/visual evidence. [Extraction preparation](implementation/evidence/P1/EXTRACTION_PREPARATION.md) records compatibility guards; no SDK import or runtime game integration occurred.

Exact SDK command vectors, exit codes and TRX counters are in `implementation/evidence/P1/results.json`; raw output is alongside it. `rtk proxy python docs/implementation/evidence/verify_p1.py` completed all checks with exit 0:

- Architecture validator passed; Python validator tests 19/19 and manifest tests 14/14 passed. Manifest: 52 tasks, 16 feature ledgers, 21 conservative waves.
- Locked restore and Release build passed with zero errors/warnings. Full .NET suite: 31 executed/passed, 0 failed/skipped; diagnostics filter: 12 executed/passed, 0 failed/skipped.
- Package listing confirms runtime NuGet dependencies remain absent. Local packaging produced 15 NuGet files and 15 managed DLLs, no native bundle; nothing published. Diff check passed.
- Separate `rtk proxy pwsh -NoProfile -File integration/unity/sqlite-qualification/run-desktop-probe.ps1`: exit 0, real Windows native SQLite 3.50.1 / .NET 9.0.4 x64; see CL-006 raw probe output and coordinator review.
- MrSquare source-linked Platform 14/14 and GameplayDomain 27/27 passed; documentation unit tests 57/57 and 29-node architecture validation passed. Unity documentation validator fails on 123 pre-existing stale maintenance move-map paths; baseline validator/map content matches incorporated main. This remains an explicit failure, not a waived pass.

All sixteen product features remain stubbed and MrSquare remains not_installed in the feature ledger. A01–A13 production acceptance remains pending; scoped desktop tooling/redaction/native-probe evidence above does not pass those whole gates. Unity 6000.5.3f1, EditMode/PlayMode/effective settings, IL2CPP/AOT/stripping, physical devices, real peer C#/TS codecs, PostgreSQL/live bootstrap/sync/rewards, provider sandboxes and independent reuse were not run. G1–G4 remain unpassed. No main merge, deployment, Firebase distribution, package publication, production migration, secret change or backend write occurred.

## Historical M0 record

Date: 2026-09-17. Milestone: M0 compatibility/tooling.

## Implemented in M0

- Fifteen `netstandard2.1` project seams and one .NET 9 xUnit project.
- Central build/package configuration, deterministic builds and genuine NuGet lockfile workflow.
- Explicit typed unavailable results/exceptions for all sixteen product features and the HTTP, SQLite, MessagePack and JSON adapters.
- Machine-readable architecture/feature catalogs, validator and validator self-tests.
- Backend-owned `0.1.0-draft` protocol mirror with SHA-256 pins.
- Documentation, scoped agent instructions, CI, PR checklist, security/private-license guidance and local non-publishing package tooling.

## Not implemented or accepted

All product features, identity/provisioning, local durability, synchronization, HTTP, codecs, native SQLite, navigation behavior and presenters remain stubs/planned. A01–A13 production gates remain pending. No Unity import, IL2CPP/AOT/stripping, device, PostgreSQL, Supabase/Deno, Workers, Node/container or cross-language MessagePack runtime test was run. No package was published and the Unity game was not modified.

## M0 validation record

Environment: Windows 10.0.26200, .NET SDK 9.0.203/MSBuild 17.13.20, Python 3.10.4 and Git 2.47.0.windows.2.

- `python scripts/validate.py`: passed; 15 runtime projects, 16 stubbed features, architecture references/module READMEs and 2 pinned contract files.
- `python -m unittest discover -s scripts -p 'test_*.py'`: 7 passed in 0.024 seconds.
- `dotnet restore GamePlatform.sln --use-lock-file`: passed; generated 16 genuine `packages.lock.json` files (15 runtime + test).
- `dotnet restore GamePlatform.sln --locked-mode`: passed with 0 errors and 0 warnings.
- `dotnet build GamePlatform.sln -c Release --no-restore`: passed with 0 errors and 0 warnings. An earlier authoring run failed because the test project inherited C# 9 while its generated xUnit global using required C# 10; the test project now explicitly uses the installed latest language version while runtime projects remain C# 9.
- `dotnet test GamePlatform.sln -c Release --no-build --no-restore`: 19 test cases passed, 0 failed/skipped/warnings, one test project, 989 ms.
- `dotnet list GamePlatform.sln package --include-transitive`: all 15 runtime projects have no NuGet packages; the test project resolved the centrally pinned test packages and lockfile-recorded transitives.
- `python scripts/package-sdk.py`: passed after correcting directory enumeration; produced 15 local `.nupkg` files, 15 managed DLLs and a SHA-256 dependency manifest. It included no native libraries and published nothing.
- Contract SHA-256: policy `3529f3d92ee3e368e91c6d1ca9176022ebe5ae5124b5072cdb76d2fd8474047c`; primitive vectors `d76ccd7aa7e26a4a8b2603061f2709e6391dcc9abfe85bd5739fb58138da30a5`.

## Publication

The private repository is published at `https://github.com/gindemit/game-platform-dotnet` with default branch `main`. The reviewed bootstrap commit is `9c7ceca148a658fffaf30e6b43388184a6140f6d`; a follow-up status-only commit records publication. There were no repository-creation, push, local-filesystem or package-feed permission blockers.
