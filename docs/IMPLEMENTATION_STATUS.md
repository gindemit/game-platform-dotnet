# Implementation status

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
